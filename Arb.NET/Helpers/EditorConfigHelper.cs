using System.Text;
using System.Text.RegularExpressions;

namespace Arb.NET;

// !AI GENERATED CODE!

public static class EditorConfigHelper {
    public static string? ResolveNewLine(string filePath) =>
        NewLineHelper.FromEndOfLine(GetProperty(filePath, "end_of_line"));

    public static string? GetProperty(string filePath, string key) {
        string fullPath = Path.GetFullPath(filePath);

        List<string> configs = [];
        string? dir = Path.GetDirectoryName(fullPath);
        while (dir != null) {
            string candidate = Path.Combine(dir, ".editorconfig");
            if (File.Exists(candidate)) {
                configs.Add(candidate);
                if (IsRoot(candidate)) break;
            }

            dir = Path.GetDirectoryName(dir);
        }

        string? value = null;
        for (int i = configs.Count - 1; i >= 0; i--) {
            string? found = ReadProperty(configs[i], fullPath, key);
            if (found != null) value = found;
        }

        return string.Equals(value, "unset", StringComparison.OrdinalIgnoreCase) ? null : value;
    }

    private static bool IsRoot(string configPath) {
        foreach (string raw in SafeReadLines(configPath)) {
            string line = raw.Trim();
            if (line.StartsWith("[")) return false; // preamble ended
            if (TryParseKeyValue(line, out string k, out string v) && k == "root") {
                return string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        return false;
    }

    private static string? ReadProperty(string configPath, string fullFilePath, string key) {
        string configDir = Path.GetDirectoryName(configPath)!;
        string relative = fullFilePath.Substring(configDir.Length).TrimStart('\\', '/').Replace('\\', '/');

        string? result = null;
        bool sectionApplies = false;

        foreach (string raw in SafeReadLines(configPath)) {
            string line = raw.Trim();
            if (line.Length == 0 || line[0] == '#' || line[0] == ';') continue;

            if (line[0] == '[' && line[line.Length - 1] == ']') {
                sectionApplies = SectionMatches(line.Substring(1, line.Length - 2), relative);
                continue;
            }

            if (sectionApplies && TryParseKeyValue(line, out string k, out string v) && k == key) {
                result = v;
            }
        }

        return result;
    }

    private static bool TryParseKeyValue(string line, out string key, out string value) {
        key = value = string.Empty;
        int eq = line.IndexOf('=');
        if (eq <= 0 || line[0] == '#' || line[0] == ';') return false;

        key = line.Substring(0, eq).Trim().ToLowerInvariant();
        value = line.Substring(eq + 1).Trim().ToLowerInvariant();
        return true;
    }

    private static IEnumerable<string> SafeReadLines(string path) {
        try {
            return File.ReadAllLines(path);
        }
        catch {
            return [];
        }
    }

    private static bool SectionMatches(string glob, string relativePath) {
        if (glob.StartsWith("/")) {
            glob = glob.Substring(1);
        }
        else if (!glob.Contains("/")) {
            glob = "**/" + glob;
        }

        try {
            return Regex.IsMatch(relativePath, "^" + GlobToRegex(glob) + "$", RegexOptions.CultureInvariant);
        }
        catch (ArgumentException) {
            return false;
        }
    }

    private static string GlobToRegex(string glob) {
        StringBuilder sb = new();
        int braceDepth = 0;

        for (int i = 0; i < glob.Length; i++) {
            char c = glob[i];
            switch (c) {
                case '*':
                    if (i + 1 < glob.Length && glob[i + 1] == '*') {
                        i++;
                        if (i + 1 < glob.Length && glob[i + 1] == '/') {
                            i++;
                            sb.Append("(?:.*/)?");
                        }
                        else {
                            sb.Append(".*");
                        }
                    }
                    else {
                        sb.Append("[^/]*");
                    }

                    break;
                case '?':
                    sb.Append("[^/]");
                    break;
                case '{': {
                    int close = glob.IndexOf('}', i);
                    string inner = close > i ? glob.Substring(i + 1, close - i - 1) : string.Empty;
                    Match range = Regex.Match(inner, @"^(-?\d+)\.\.(-?\d+)$");
                    if (range.Success) {
                        sb.Append(@"-?\d+");
                        i = close;
                    }
                    else if (close > i && inner.Contains(",")) {
                        braceDepth++;
                        sb.Append("(?:");
                    }
                    else {
                        sb.Append(@"\{");
                    }

                    break;
                }
                case '}':
                    if (braceDepth > 0) {
                        braceDepth--;
                        sb.Append(')');
                    }
                    else {
                        sb.Append(@"\}");
                    }

                    break;
                case ',':
                    sb.Append(braceDepth > 0 ? "|" : ",");
                    break;
                case '[': {
                    int close = glob.IndexOf(']', i + 1);
                    if (close > i) {
                        string set = glob.Substring(i + 1, close - i - 1);
                        if (set.StartsWith("!")) set = "^" + set.Substring(1);
                        sb.Append('[').Append(set.Replace("\\", "\\\\")).Append(']');
                        i = close;
                    }
                    else {
                        sb.Append(@"\[");
                    }

                    break;
                }
                case '\\':
                    if (i + 1 < glob.Length) {
                        sb.Append(Regex.Escape(glob[++i].ToString()));
                    }

                    break;
                default:
                    sb.Append(Regex.Escape(c.ToString()));
                    break;
            }
        }

        return sb.ToString();
    }
}
