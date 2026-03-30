using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Arb.NET.Models;

namespace Arb.NET.Tool.Migration;

/// <summary>
/// Migrates .resx files to .arb format.
/// </summary>
internal static class ResxMigrator
{
    // Matches {0}, {1:N2}, {0,10} style format items
    private static readonly Regex FORMAT_ITEM_REGEX = new(@"\{(\d+)(?:,[^}]*)?(:[^}]*)?\}", RegexOptions.Compiled);

    /// <summary>
    /// Discovers all .resx file groups under <paramref name="sourceFolder"/>, converts them to
    /// <see cref="ArbDocument"/> instances, and writes .arb files into a single output directory
    /// derived from <paramref name="outputFolder"/> and <paramref name="config"/>.
    /// A l10n.yaml is written to <paramref name="outputFolder"/>.
    /// </summary>
    public static MigrationResult Migrate(string sourceFolder, string outputFolder, L10nConfig config, bool dryRun,
                                          bool deduplicate = false)
    {
        MigrationResult result = new();
        List<ResxGroup> groups = DiscoverResxGroups(sourceFolder);

        string arbsDir = Path.Combine(outputFolder, config.ArbDir);

        // Derive the default locale from the template-arb-file config (e.g. "en.arb" → "en", "cs.arb" → "cs").
        string defaultLocale = Path.GetFileNameWithoutExtension(config.TemplateArbFile);

        // Phase 1: parse each group into per-locale entries, keyed by (baseStem, locale, arbKey).
        // Structure: locale → arbKey → list of (baseStem, ArbEntry)
        Dictionary<string, Dictionary<string, List<(string BaseStem, ArbEntry Entry)>>> byLocale =
            new(StringComparer.OrdinalIgnoreCase);

        foreach (ResxGroup group in groups)
        {
            try
            {
                CollectGroup(group, byLocale, defaultLocale);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Failed to parse group '{group.BaseName}': {ex.Message}");
            }
        }

        // Phase 2: resolve collisions and build final ArbDocuments.
        Dictionary<string, ArbDocument> docsByLocale = new(StringComparer.OrdinalIgnoreCase);

        foreach ((string locale, Dictionary<string, List<(string BaseStem, ArbEntry Entry)>> keyMap) in byLocale)
        {
            ArbDocument doc = new() { Locale = locale };
            docsByLocale[locale] = doc;

            foreach ((string arbKey, List<(string BaseStem, ArbEntry Entry)> candidates) in keyMap)
            {
                if (candidates.Count == 1)
                {
                    // No collision — use as-is.
                    doc.Entries[arbKey] = candidates[0].Entry;
                }
                else if (deduplicate && AllIdenticalAcrossLocales(arbKey, byLocale))
                {
                    // All locales agree on the same value — keep one entry, no suffix.
                    doc.Entries[arbKey] = candidates[0].Entry;
                }
                else
                {
                    // Collision: suffix each entry with its source base stem.
                    foreach ((string baseStem, ArbEntry entry) in candidates)
                    {
                        string suffixedKey = arbKey + "_" + ToArbKey(baseStem);
                        entry.Key = suffixedKey;
                        doc.Entries[suffixedKey] = entry;
                    }
                }
            }
        }

        // Write arb files
        foreach ((string locale, ArbDocument doc) in docsByLocale)
        {
            string outPath = Path.Combine(arbsDir, $"{locale}.arb");
            WriteArb(doc, outPath, dryRun, result);
        }

        // Write l10n.yaml
        WriteL10nYaml(outputFolder, config, dryRun, result);

        return result;
    }

    /// <summary>
    /// Returns true when, in every locale that contains <paramref name="arbKey"/>,
    /// all candidates (from different source groups) agree on the same value.
    /// Values may differ between locales — that is expected and correct.
    /// </summary>
    private static bool AllIdenticalAcrossLocales(
        string arbKey,
        Dictionary<string, Dictionary<string, List<(string BaseStem, ArbEntry Entry)>>> byLocale)
    {
        foreach (Dictionary<string, List<(string BaseStem, ArbEntry Entry)>> keyMap in byLocale.Values)
        {
            if (!keyMap.TryGetValue(arbKey, out List<(string BaseStem, ArbEntry Entry)>? candidates)) continue;

            // All candidates within this locale must have the same value.
            string? localeValue = null;
            foreach ((_, ArbEntry entry) in candidates)
            {
                if (localeValue == null) {
                    localeValue = entry.Value;
                }
                else if (entry.Value != localeValue) {
                    return false;
                }
            }
        }

        return true;
    }

    private static List<ResxGroup> DiscoverResxGroups(string sourceFolder)
    {
        List<string> allResx = Directory.EnumerateFiles(sourceFolder, "*.resx", SearchOption.AllDirectories)
            .ToList();

        // Group files: canonical name is the file without the locale suffix.
        // E.g. "Strings.fr.resx" → base = "Strings.resx", locale = "fr"
        //      "Strings.resx"    → base = "Strings.resx", locale = null (default)
        Dictionary<string, ResxGroup> map = new(StringComparer.OrdinalIgnoreCase);

        foreach (string file in allResx)
        {
            string dir = Path.GetDirectoryName(file)!;
            string fileName = Path.GetFileNameWithoutExtension(file); // e.g. "Strings.fr" or "Strings"

            // Check if the file name contains a locale part (e.g. "Strings.fr")
            int lastDot = fileName.LastIndexOf('.');
            string? locale = null;
            string baseStem;

            if (lastDot >= 0)
            {
                string possibleLocale = fileName[(lastDot + 1)..];
                if (IsLocaleCode(possibleLocale))
                {
                    locale = possibleLocale;
                    baseStem = fileName[..lastDot];
                }
                else
                {
                    baseStem = fileName;
                }
            }
            else
            {
                baseStem = fileName;
            }

            // Detect non-standard compound locale: BaseName_XX.yy.resx
            // where _XX is an uppercase locale code embedded in the base stem.
            // E.g. "Variant_CS.en.resx" → base = "Variant", locale = "cs_en"
            int lastUnderscore = baseStem.LastIndexOf('_');
            if (lastUnderscore >= 0 && locale != null)
            {
                string possibleEmbeddedLocale = baseStem[(lastUnderscore + 1)..];
                if (IsLocaleCode(possibleEmbeddedLocale))
                {
                    locale = possibleEmbeddedLocale.ToLowerInvariant() + "_" + locale;
                    baseStem = baseStem[..lastUnderscore];
                }
            }

            string baseKey = Path.Combine(dir, baseStem + ".resx");

            if (!map.TryGetValue(baseKey, out ResxGroup? group))
            {
                group = new ResxGroup(dir, baseStem);
                map[baseKey] = group;
            }

            if (locale == null) {
                group.DefaultFile = file;
            }
            else {
                group.LocaleFiles[locale] = file;
            }
        }

        return map.Values.ToList();
    }

    // Very lightweight locale code heuristic: 2-letter, 2+2 (zh-CN), 3-letter (zho), etc.
    private static bool IsLocaleCode(string s) =>
        Regex.IsMatch(s, @"^[a-zA-Z]{2,3}(-[a-zA-Z]{2,4})?$");

    private static void CollectGroup(
        ResxGroup group,
        Dictionary<string, Dictionary<string, List<(string BaseStem, ArbEntry Entry)>>> byLocale,
        string defaultLocale)
    {
        // The default (neutral) file maps to whatever the project's primary language is.
        // Collect it first so that an explicit locale .resx can overwrite shared keys within the group.
        if (group.DefaultFile != null) {
            CollectResxInto(group.BaseStem, NormalizeLocale(defaultLocale), group.DefaultFile, byLocale);
        }

        foreach ((string locale, string file) in group.LocaleFiles) {
            CollectResxInto(group.BaseStem, NormalizeLocale(locale), file, byLocale);
        }
    }

    private static void CollectResxInto(
        string baseStem,
        string locale,
        string filePath,
        Dictionary<string, Dictionary<string, List<(string BaseStem, ArbEntry Entry)>>> byLocale)
    {
        if (!byLocale.TryGetValue(locale, out Dictionary<string, List<(string, ArbEntry)>>? keyMap))
        {
            keyMap = new Dictionary<string, List<(string, ArbEntry)>>(StringComparer.Ordinal);
            byLocale[locale] = keyMap;
        }

        XDocument xdoc = XDocument.Load(filePath);

        // Track keys already seen from this specific (baseStem, locale) pair so the base file
        // can be overwritten by the explicit locale file within the same group.
        HashSet<string> seenInThisFile = [];

        foreach (XElement data in xdoc.Root?.Elements("data") ?? [])
        {
            string? name = data.Attribute("name")?.Value;
            string? value = data.Element("value")?.Value;
            string? comment = data.Element("comment")?.Value;

            if (string.IsNullOrEmpty(name) || value == null) continue;

            string arbKey = ToArbKey(name);
            string arbValue = ConvertFormatString(value, out List<string> placeholderNames);

            ArbEntry entry = new() { Key = arbKey, Value = arbValue };

            if (comment != null || placeholderNames.Count > 0)
            {
                ArbMetadata metadata = new();
                if (comment != null) {
                    metadata.Description = comment.Trim();
                }
                if (placeholderNames.Count > 0)
                {
                    metadata.Placeholders = [];
                    foreach (string ph in placeholderNames) {
                        metadata.Placeholders[ph] = new ArbPlaceholder { Type = "String" };
                    }
                }
                entry.Metadata = metadata;
            }

            if (!keyMap.TryGetValue(arbKey, out List<(string BaseStem, ArbEntry Entry)>? candidates))
            {
                candidates = [];
                keyMap[arbKey] = candidates;
            }

            // Within the same group, overwrite the entry from the same baseStem
            // (e.g. base file followed by explicit locale file for the same group).
            int existingIdx = candidates.FindIndex(c => c.BaseStem == baseStem);
            if (existingIdx >= 0) {
                candidates[existingIdx] = (baseStem, entry);
            }
            else {
                candidates.Add((baseStem, entry));
            }

            seenInThisFile.Add(arbKey);
        }
    }

    /// <summary>
    /// Converts a .NET composite format string (e.g. "Hello {0}, you have {1} messages")
    /// to an ARB-style named placeholder string ("Hello {name0}, you have {name1} messages"),
    /// collecting the placeholder names used.
    /// </summary>
    private static string ConvertFormatString(string value, out List<string> placeholderNames)
    {
        placeholderNames = [];
        Dictionary<int, string> nameMap = new(); // index → arb name

        // First pass: collect all unique indices
        foreach (Match m in FORMAT_ITEM_REGEX.Matches(value))
        {
            int index = int.Parse(m.Groups[1].Value);
            if (nameMap.ContainsKey(index)) continue;
            string phName = $"param{index}";
            nameMap[index] = phName;
        }

        if (nameMap.Count == 0) {
            return value;
        }

        // Replace from right to left to preserve indices
        string result = FORMAT_ITEM_REGEX.Replace(value, m =>
        {
            int index = int.Parse(m.Groups[1].Value);
            return $"{{{nameMap[index]}}}";
        });

        placeholderNames = nameMap.OrderBy(kv => kv.Key).Select(kv => kv.Value).ToList();
        return result;
    }

    // -------------------------------------------------------------------------
    // Key conversion
    // -------------------------------------------------------------------------

    /// <summary>
    /// Converts a .resx resource name to a camelCase ARB key.
    /// e.g. "MyResource_Name" → "myResourceName"
    /// </summary>
    private static string ToArbKey(string name)
    {
        string[] parts = name.Split(['_', '.'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return name;

        StringBuilder sb = new();
        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i];
            if (i == 0) {
                sb.Append(char.ToLowerInvariant(part[0]) + part[1..]);
            }
            else {
                sb.Append(char.ToUpperInvariant(part[0]) + part[1..]);
            }
        }

        return sb.ToString();
    }

    // ARB uses underscores (en_US) while .resx uses hyphens (en-US).
    private static string NormalizeLocale(string locale) => locale.Replace('-', '_');

    // -------------------------------------------------------------------------
    // Output
    // -------------------------------------------------------------------------

    private static void WriteArb(ArbDocument doc, string outputPath, bool dryRun, MigrationResult result)
    {
        string content = ArbSerializer.Serialize(doc);

        if (dryRun)
        {
            result.PlannedWrites.Add(outputPath);
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllText(outputPath, content, Constants.UTF8_NO_BOM);
            result.WrittenFiles.Add(outputPath);
        }
    }

    private static void WriteL10nYaml(string outputFolder, L10nConfig config, bool dryRun, MigrationResult result)
    {
        string yamlPath = Path.Combine(outputFolder, Constants.LOCALIZATION_FILE);

        StringBuilder sb = new();
        sb.AppendLine($"arb-dir: {config.ArbDir}");
        sb.AppendLine($"template-arb-file: {config.TemplateArbFile}");
        if (config.OutputClass != null) {
            sb.AppendLine($"output-class: {config.OutputClass}");
        }
        if (config.OutputNamespace != null) {
            sb.AppendLine($"output-namespace: {config.OutputNamespace}");
        }

        string content = sb.ToString();

        if (dryRun)
        {
            result.PlannedWrites.Add(yamlPath);
        }
        else
        {
            Directory.CreateDirectory(outputFolder);
            File.WriteAllText(yamlPath, content, Constants.UTF8_NO_BOM);
            result.WrittenFiles.Add(yamlPath);
        }
    }
}
