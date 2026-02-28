using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Arb.NET.IDE.Common.Models;
using Arb.NET.Models;

namespace Arb.NET.IDE.Common.Services;

/// <summary>
/// Shared logic for discovering ARB keys from a project directory.
/// Used by both the Visual Studio and JetBrains IDE integrations.
/// </summary>
public static class ArbKeyService {
    // TODO(naive) assumes generator stability
    private static readonly Regex PUBLIC_PROPERTY_REGEX =
        new(@"^\s+public\s+string\s+(\w+)\s*=>", RegexOptions.Multiline | RegexOptions.Compiled);

    // TODO(naive) assumes generator stability
    private static readonly Regex PUBLIC_METHOD_REGEX =
        new(@"^\s+public\s+string\s+(\w+)\s*\(", RegexOptions.Multiline | RegexOptions.Compiled);

    // Matches a run of XML doc-comment lines immediately before a `public string KeyName` declaration.
    private static readonly Regex XML_DOC_BLOCK_REGEX =
        new(@"((?:[ \t]*/// .*\r?\n)+)[ \t]*public string (\w+)", RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly ConcurrentDictionary<string, CacheEntry> CACHE =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Walks up the directory tree from <paramref name="filePath"/> and returns the first
    /// directory that contains an <c>l10n.yaml</c> file, or <c>null</c> if none is found.
    /// </summary>
    public static string? FindProjectDirFromFilePath(string? filePath) {
        if (string.IsNullOrWhiteSpace(filePath)) return null;

        string? currentDirectory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(currentDirectory)) return null;

        DirectoryInfo? dir = new(currentDirectory);
        while (dir != null) {
            // TODO(l10n) This is naive; the l10n.yaml may exist elsewhere (e.g. a directory with the arb files)
            // TODO(magic)
            if (File.Exists(Path.Combine(dir.FullName, "l10n.yaml"))) {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }

    /// <summary>
    /// Returns all ARB keys for the project rooted at <paramref name="projectDir"/>.
    /// Primary path: scans the generated <c>*Dispatcher.g.cs</c> file.
    /// Fallback: parses the template <c>.arb</c> file directly.
    /// Results are cached and invalidated when <c>l10n.yaml</c> or the template <c>.arb</c> file changes.
    /// </summary>
    public static List<ArbKeyInfo> GetKeys(string projectDir) {
        // TODO(l10n) This is naive; the l10n.yaml may exist elsewhere (e.g. a directory with the arb files)
        // TODO(magic)
        string localizationPath = Path.Combine(projectDir, "l10n.yaml");
        if (!File.Exists(localizationPath)) return [];

        L10nConfig config;
        try {
            config = L10nConfig.Parse(File.ReadAllText(localizationPath));
        }
        catch {
            return [];
        }

        // TODO(cleanup) Output class should be a required config value. No need to fallback here.
        // TODO(magic)
        string outputClass = config.OutputClass ?? "AppLocale";

        string arbPath = string.IsNullOrWhiteSpace(config.TemplateArbFile)
            ? string.Empty
            : Path.Combine(projectDir, config.ArbDir, config.TemplateArbFile);

        DateTime localizationStamp = SafeLastWriteUtc(localizationPath);
        DateTime arbStamp = string.IsNullOrWhiteSpace(arbPath) ? DateTime.MinValue : SafeLastWriteUtc(arbPath);

        if (CACHE.TryGetValue(projectDir, out CacheEntry? cached)
            && cached.LocalizationStamp == localizationStamp
            && cached.ArbStamp == arbStamp) {
            return cached.Keys;
        }

        List<ArbKeyInfo> keys = TryGetKeysFromGeneratedFile(projectDir, outputClass, arbPath);
        if (keys.Count == 0) {
            keys = GetKeysFromArbFile(arbPath);
        }

        CACHE[projectDir] = new CacheEntry(localizationStamp, arbStamp, keys);
        return keys;
    }

    /// <summary>
    /// Reads all non-metadata key names from an .arb file and maps them to their 0-based line index.
    /// Returns an empty dictionary if the file cannot be read.
    /// </summary>
    private static Dictionary<string, int> GetKeyLineNumbers(string arbPath) {
        Dictionary<string, int> result = new();
        try {
            string[] lines = File.ReadAllLines(arbPath);
            for (int i = 0; i < lines.Length; i++) {
                string trimmed = lines[i].TrimStart();
                if (!trimmed.StartsWith("\"")) continue;
                int closeQuote = trimmed.IndexOf('"', 1);
                if (closeQuote <= 1) continue;
                string candidate = trimmed.Substring(1, closeQuote - 1);
                if (candidate.StartsWith("@")) continue;
                string afterClose = trimmed.Substring(closeQuote + 1).TrimStart();
                if (!afterClose.StartsWith(":")) continue;
                result[candidate] = i;
            }
        }
        catch {
            // Best-effort; callers tolerate missing line numbers.
        }

        return result;
    }

    /// <summary>
    /// Scans the generated dispatcher file content and returns a map of PascalCase key name →
    /// raw inner content of the &lt;summary&gt; XML doc tag (with leading <c>/// </c> prefixes stripped).
    /// </summary>
    private static Dictionary<string, string> ExtractXmlDocs(string content) {
        Dictionary<string, string> result = new(StringComparer.Ordinal);
        foreach (Match m in XML_DOC_BLOCK_REGEX.Matches(content)) {
            string docBlock = m.Groups[1].Value;
            string keyName = m.Groups[2].Value;

            // Strip the leading `    /// ` (or similar) prefix from each line
            string[] lines = docBlock.Split('\n');
            IEnumerable<string> stripped = lines
                .Select(l => Regex.Replace(l, @"^[ \t]*/// ?", "").TrimEnd('\r'))
                .Where(l => l.Length > 0);
            string joined = string.Join("\n", stripped);

            // Extract content between <summary> and </summary>
            int start = joined.IndexOf("<summary>", StringComparison.Ordinal);
            int end = joined.IndexOf("</summary>", StringComparison.Ordinal);
            if (start < 0 || end < 0 || end <= start) continue;

            string inner = joined.Substring(start + "<summary>".Length, end - start - "<summary>".Length).Trim();
            if (!string.IsNullOrEmpty(inner)) {
                result[keyName] = inner;
            }
        }
        return result;
    }

    private static List<ArbKeyInfo> TryGetKeysFromGeneratedFile(string projectDir, string outputClass, string arbPath) {
        // Build PascalCase → description/rawKey and PascalCase → lineNumber lookups from the template .arb
        Dictionary<string, string?> descriptions = new();
        Dictionary<string, string> rawKeys = new();
        Dictionary<string, int> pascalLineNumbers = new();
        if (File.Exists(arbPath)) {
            Dictionary<string, int> rawLineNumbers = GetKeyLineNumbers(arbPath);
            try {
                ArbParseResult arbResult = new ArbParser().Parse(arbPath);
                if (arbResult.Document != null) {
                    foreach (KeyValuePair<string, ArbEntry> kvp in arbResult.Document.Entries) {
                        string pk = StringHelper.ToPascalCase(kvp.Key);
                        descriptions[pk] = kvp.Value.Metadata?.Description;
                        rawKeys[pk] = kvp.Key;
                    }
                }
            }
            catch {
                // Ignore; descriptions stay empty.
            }

            foreach (KeyValuePair<string, int> kv in rawLineNumbers) {
                pascalLineNumbers[StringHelper.ToPascalCase(kv.Key)] = kv.Value;
            }
        }

        // TODO(naive) This assumes the generated file is named exactly {OutputClass}Dispatcher.g.cs and is located
        // somewhere under the project directory. True for the default config, but may not be in general.
        // TODO(magic)
        IEnumerable<string> candidates;
        try {
            candidates = Directory.EnumerateFiles(
                projectDir, $"{outputClass}Dispatcher.g.cs", SearchOption.AllDirectories);
        }
        catch {
            return [];
        }

        string? resolvedArbPath = File.Exists(arbPath) ? arbPath : null;

        foreach (string filePath in candidates) {
            try {
                string content = File.ReadAllText(filePath);
                Dictionary<string, string> xmlDocs = ExtractXmlDocs(content);
                List<ArbKeyInfo> result = [];

                // Properties → non-parametric
                foreach (Match m in PUBLIC_PROPERTY_REGEX.Matches(content)) {
                    string name = m.Groups[1].Value;
                    // TODO(magic) skip compiler-generated and framework members
                    if (name is "ResolveLocale" or "TryParent") continue;
                    descriptions.TryGetValue(name, out string? desc);
                    rawKeys.TryGetValue(name, out string? rawKey);
                    int ln = pascalLineNumbers.TryGetValue(name, out int lineNum) ? lineNum : -1;
                    xmlDocs.TryGetValue(name, out string? xmlDoc);
                    result.Add(new ArbKeyInfo(name, false, desc, resolvedArbPath, rawKey, ln, xmlDoc));
                }

                // Methods → parametric (skip constructors)
                foreach (Match m in PUBLIC_METHOD_REGEX.Matches(content)) {
                    string name = m.Groups[1].Value;
                    if (name == outputClass) continue; // constructor
                    // TODO(magic) skip compiler-generated and framework members
                    if (name is "ResolveLocale" or "TryParent") continue;
                    descriptions.TryGetValue(name, out string? desc);
                    rawKeys.TryGetValue(name, out string? rawKey);
                    int ln = pascalLineNumbers.TryGetValue(name, out int lineNum) ? lineNum : -1;
                    xmlDocs.TryGetValue(name, out string? xmlDoc);
                    result.Add(new ArbKeyInfo(name, true, desc, resolvedArbPath, rawKey, ln, xmlDoc));
                }

                if (result.Count > 0) {
                    return result
                        .GroupBy(x => x.Key, StringComparer.Ordinal)
                        .Select(g => g.First())
                        .OrderBy(x => x.Key, StringComparer.Ordinal)
                        .ToList();
                }
            }
            catch {
                // Ignore this candidate and try next.
            }
        }

        return [];
    }

    private static List<ArbKeyInfo> GetKeysFromArbFile(string arbPath) {
        if (string.IsNullOrWhiteSpace(arbPath)) return [];
        if (!File.Exists(arbPath)) return [];

        try {
            ArbParseResult result = new ArbParser().Parse(arbPath);
            if (result.Document == null) return [];

            Dictionary<string, int> lineNumbers = GetKeyLineNumbers(arbPath);

            return result.Document.Entries
                .Select(kvp => {
                    bool isParametric = kvp.Value.IsParametric(out _);
                    string pascalKey = StringHelper.ToPascalCase(kvp.Key);
                    string? description = kvp.Value.Metadata?.Description;
                    int lineNumber = lineNumbers.TryGetValue(kvp.Key, out int ln) ? ln : -1;
                    return new ArbKeyInfo(pascalKey, isParametric, description, arbPath, kvp.Key, lineNumber);
                })
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .ToList();
        }
        catch {
            return [];
        }
    }

    private static DateTime SafeLastWriteUtc(string path) {
        try {
            return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
        }
        catch {
            return DateTime.MinValue;
        }
    }

    private sealed class CacheEntry(DateTime localizationStamp, DateTime arbStamp, List<ArbKeyInfo> keys) {
        public DateTime LocalizationStamp { get; } = localizationStamp;
        public DateTime ArbStamp { get; } = arbStamp;
        public List<ArbKeyInfo> Keys { get; } = keys;
    }
}