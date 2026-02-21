using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Arb.NET.Tool.Migration;

/// <summary>
/// Migrates .resx files to .arb format.
/// </summary>
internal static class ResxMigrator
{
    // Matches {0}, {1:N2}, {0,10} style format items
    private static readonly Regex FORMAT_ITEM_REGEX = new(@"\{(\d+)(?:,[^}]*)?(:[^}]*)?\}", RegexOptions.Compiled);

    /// <summary>
    /// Discovers all .resx file groups under <paramref name="solutionFolder"/>, converts them to
    /// <see cref="ArbDocument"/> instances, and writes .arb files next to the originals.
    /// </summary>
    public static MigrationResult Migrate(string solutionFolder, bool dryRun)
    {
        MigrationResult result = new();
        List<ResxGroup> groups = DiscoverResxGroups(solutionFolder);

        // Multiple .resx groups in the same directory share the same arbs/ output folder,
        // so their entries must be merged into a single ArbDocument per locale before writing.
        IEnumerable<IGrouping<string, ResxGroup>> byOutputDir = groups.GroupBy(g => Path.Combine(g.Directory, "arbs"),
                                                                               StringComparer.OrdinalIgnoreCase);

        foreach (IGrouping<string, ResxGroup> dirGroup in byOutputDir)
        {
            try
            {
                MigrateOutputDir(dirGroup.Key, dirGroup, dryRun, result);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Failed to migrate '{dirGroup.Key}': {ex.Message}");
            }
        }

        return result;
    }

    // -------------------------------------------------------------------------
    // Discovery
    // -------------------------------------------------------------------------

    private static List<ResxGroup> DiscoverResxGroups(string solutionFolder)
    {
        List<string> allResx = Directory.EnumerateFiles(solutionFolder, "*.resx", SearchOption.AllDirectories)
            .Where(f => !IsInBinOrObj(f))
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

            string baseKey = Path.Combine(dir, baseStem + ".resx");

            if (!map.TryGetValue(baseKey, out var group))
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

    private static bool IsInBinOrObj(string path)
    {
        string[] parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(p => p.Equals("bin", StringComparison.OrdinalIgnoreCase)
                           || p.Equals("obj", StringComparison.OrdinalIgnoreCase));
    }

    // Very lightweight locale code heuristic: 2-letter, 2+2 (zh-CN), 3-letter (zho), etc.
    private static bool IsLocaleCode(string s) =>
        Regex.IsMatch(s, @"^[a-zA-Z]{2,3}(-[a-zA-Z]{2,4})?$");

    // -------------------------------------------------------------------------
    // Group migration
    // -------------------------------------------------------------------------

    private static void MigrateOutputDir(string outputDir, IEnumerable<ResxGroup> groups, bool dryRun, MigrationResult result)
    {
        // Accumulate entries per locale across all groups in this output directory.
        Dictionary<string, ArbDocument> docsByLocale = new(StringComparer.OrdinalIgnoreCase);

        ArbDocument GetOrCreate(string locale)
        {
            if (!docsByLocale.TryGetValue(locale, out var doc))
            {
                doc = new ArbDocument { Locale = locale };
                docsByLocale[locale] = doc;
            }
            return doc;
        }

        foreach (ResxGroup group in groups)
        {
            // Collect the set of locales covered by explicit locale files in this group.
            HashSet<string> explicitLocales = new(group.LocaleFiles.Keys, StringComparer.OrdinalIgnoreCase);

            // The default (neutral) file maps to "en", but only when no explicit "en" file exists.
            if (group.DefaultFile != null && !explicitLocales.Contains("en")) {
                MergeResxInto(GetOrCreate("en"), group.DefaultFile);
            }

            foreach ((string locale, string file) in group.LocaleFiles) {
                MergeResxInto(GetOrCreate(locale), file);
            }
        }

        foreach ((string locale, ArbDocument doc) in docsByLocale)
        {
            string outPath = Path.Combine(outputDir, $"{locale}.arb");
            WriteArb(doc, outPath, dryRun, result);
        }
    }

    // -------------------------------------------------------------------------
    // Parsing
    // -------------------------------------------------------------------------

    private static void MergeResxInto(ArbDocument doc, string filePath)
    {
        XDocument xdoc = XDocument.Load(filePath);

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
                        metadata.Placeholders[ph] = new ArbPlaceholder {
                            Type = "String"
                        };
                    }
                }
                entry.Metadata = metadata;
            }

            doc.Entries[arbKey] = entry;
        }
    }

    // -------------------------------------------------------------------------
    // Format string conversion
    // -------------------------------------------------------------------------

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
        // Split on _, space, or PascalCase word boundaries, then rejoin as camelCase
        // Strategy: just lowercase the first character of the whole string and replace _ with nothing smart
        // Simple approach: treat underscores and dots as word separators
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
            File.WriteAllText(outputPath, content, Encoding.UTF8);
            result.WrittenFiles.Add(outputPath);
        }
    }
}