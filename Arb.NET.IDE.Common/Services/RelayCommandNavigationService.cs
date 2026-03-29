using System.Text.RegularExpressions;
using Arb.NET.IDE.Common.Models;

namespace Arb.NET.IDE.Common.Services;

public static class RelayCommandNavigationService {
    private static readonly Regex CommandBindingRegex = new(
        "Command\\s*=\\s*([\"'])(\\{Binding.*?\\})\\1",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex BindingPathRegex = new(
        @"(?:^|[\s,])Path\s*=\s*(?<path>[\w.]+Command)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ImplicitBindingPathRegex = new(
        @"^\{Binding\s+(?<path>[\w.]+Command)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex XmlNamespaceRegex = new(
        "xmlns:(?<prefix>[\\w]+)\\s*=\\s*([\"'])(?<ns>[^\"']+)\\2",
        RegexOptions.Compiled);

    private static readonly Regex DataTypeRegex = new(
        "x:DataType\\s*=\\s*([\"'])(?<type>[^\"']+)\\1",
        RegexOptions.Compiled);

    private static readonly Regex XClassRegex = new(
        "x:Class\\s*=\\s*([\"'])(?<type>[^\"']+)\\1",
        RegexOptions.Compiled);

    private static readonly Regex RelayCommandMethodRegex = new(
        @"\[\s*RelayCommand(?:Attribute)?(?:\s*\([^\)]*\))?\s*\](?:\s*\[[^\]]+\])*\s*(?:(?:public|private|protected|internal|static|virtual|sealed|override|async|partial|new|extern)\s+)*[^\r\n\(]+?\s+(?<name>\w+)\s*\(",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex FileScopedNamespaceRegex = new(
        @"^\s*namespace\s+(?<ns>[\w.]+)\s*;",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex BlockNamespaceRegex = new(
        @"^\s*namespace\s+(?<ns>[\w.]+)\s*\{",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex ClassRegex = new(
        @"\b(?:partial\s+)?class\s+(?<name>\w+)\b",
        RegexOptions.Compiled);

    public static bool TryResolveCommandNavigationTarget(string xamlFilePath, string xamlText, int caretOffset, out RelayCommandNavigationTarget? target) {
        target = null;
        if (string.IsNullOrWhiteSpace(xamlFilePath) || string.IsNullOrEmpty(xamlText)) return false;

        string? methodName = TryGetRelayCommandMethodNameAtOffset(xamlText, caretOffset);
        if (string.IsNullOrWhiteSpace(methodName)) return false;

        string? xClass = GetLastAttributeValueBeforeOffset(XClassRegex, xamlText, caretOffset, "type");
        Dictionary<string, string> xmlns = GetXmlNamespaces(xamlText);
        string? preferredType = ResolveTypeName(GetLastAttributeValueBeforeOffset(DataTypeRegex, xamlText, caretOffset, "type"), xmlns, xClass);
        string? fallbackType = ResolveTypeName(xClass, xmlns, xClass);
        string companionFile = xamlFilePath + ".cs";

        string projectDir = FindNearestProjectDirectory(xamlFilePath);
        List<Candidate> candidates = FindRelayCommandCandidates(projectDir, methodName!);
        if (candidates.Count == 0) return false;

        List<ScoredCandidate> ranked = candidates
            .Select(candidate => new ScoredCandidate(candidate, ScoreCandidate(candidate, preferredType, fallbackType, companionFile)))
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Candidate.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (ranked.Count > 1 && ranked[0].Score == ranked[1].Score) return false;

        Candidate best = ranked[0].Candidate;
        target = new RelayCommandNavigationTarget(best.FilePath, best.LineNumber, best.Column, best.MethodName);
        return true;
    }

    private static string? TryGetRelayCommandMethodNameAtOffset(string xamlText, int caretOffset) {
        foreach (Match match in CommandBindingRegex.Matches(xamlText)) {
            Group expression = match.Groups[2];
            if (caretOffset < expression.Index || caretOffset > expression.Index + expression.Length) continue;

            string? path = ExtractBindingPath(expression.Value);
            if (string.IsNullOrWhiteSpace(path)) return null;
            string resolvedPath = path;

            string commandName = resolvedPath.Split('.').Last();
            if (!commandName.EndsWith("Command", StringComparison.Ordinal) || commandName.Length <= "Command".Length) {
                return null;
            }

            return commandName.Substring(0, commandName.Length - "Command".Length);
        }

        return null;
    }

    private static string? ExtractBindingPath(string expression) {
        Match named = BindingPathRegex.Match(expression);
        if (named.Success) return named.Groups["path"].Value;

        Match implicitPath = ImplicitBindingPathRegex.Match(expression.Trim());
        return implicitPath.Success ? implicitPath.Groups["path"].Value : null;
    }

    private static Dictionary<string, string> GetXmlNamespaces(string xamlText) {
        Dictionary<string, string> result = new(StringComparer.Ordinal);
        foreach (Match match in XmlNamespaceRegex.Matches(xamlText)) {
            result[match.Groups["prefix"].Value] = match.Groups["ns"].Value;
        }

        return result;
    }

    private static string? GetLastAttributeValueBeforeOffset(Regex regex, string text, int offset, string groupName) {
        string? value = null;
        foreach (Match match in regex.Matches(text)) {
            if (match.Index > offset) break;
            value = match.Groups[groupName].Value;
        }

        return value;
    }

    private static string? ResolveTypeName(string? rawType, IReadOnlyDictionary<string, string> xmlns, string? xClass) {
        if (string.IsNullOrWhiteSpace(rawType)) return null;

        string trimmed = rawType.Trim();
        if (trimmed.StartsWith("{", StringComparison.Ordinal)) return null;

        int prefixSeparator = trimmed.IndexOf(':');
        if (prefixSeparator > 0) {
            string prefix = trimmed.Substring(0, prefixSeparator);
            string typeName = trimmed.Substring(prefixSeparator + 1);
            if (!xmlns.TryGetValue(prefix, out string? nsValue)) return null;
            string resolvedNsValue = nsValue;

            string? resolvedNamespace = ResolveClrNamespace(resolvedNsValue);
            return string.IsNullOrWhiteSpace(resolvedNamespace) ? null : resolvedNamespace + "." + typeName;
        }

        if (trimmed.Contains('.')) return trimmed;

        string? fallbackNamespace = GetNamespace(xClass);
        return string.IsNullOrWhiteSpace(fallbackNamespace) ? trimmed : fallbackNamespace + "." + trimmed;
    }

    private static string? ResolveClrNamespace(string value) {
        if (value.StartsWith("clr-namespace:", StringComparison.Ordinal)) {
            return value.Substring("clr-namespace:".Length).Split(';')[0].Trim();
        }

        if (value.StartsWith("using:", StringComparison.Ordinal)) {
            return value.Substring("using:".Length).Trim();
        }

        return null;
    }

    private static string? GetNamespace(string? fullTypeName) {
        if (string.IsNullOrWhiteSpace(fullTypeName)) return null;

        int lastDot = fullTypeName.LastIndexOf('.');
        return lastDot > 0 ? fullTypeName.Substring(0, lastDot) : null;
    }

    private static string FindNearestProjectDirectory(string filePath) {
        string? startingDirectory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(startingDirectory)) return string.Empty;

        DirectoryInfo? dir = new(startingDirectory);
        while (dir != null) {
            if (dir.EnumerateFiles("*.csproj", SearchOption.TopDirectoryOnly).Any()) {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return Path.GetDirectoryName(filePath) ?? string.Empty;
    }

    private static List<Candidate> FindRelayCommandCandidates(string projectDir, string methodName) {
        if (string.IsNullOrWhiteSpace(projectDir) || !Directory.Exists(projectDir)) return [];

        List<Candidate> result = [];
        foreach (string filePath in Directory.EnumerateFiles(projectDir, "*.cs", SearchOption.AllDirectories)) {
            if (IsGeneratedOrBuildPath(filePath)) continue;

            string content;
            try {
                content = File.ReadAllText(filePath);
            }
            catch {
                continue;
            }

            foreach (Match match in RelayCommandMethodRegex.Matches(content)) {
                if (!string.Equals(match.Groups["name"].Value, methodName, StringComparison.Ordinal)) continue;

                int methodIndex = match.Groups["name"].Index;
                result.Add(new Candidate(
                    filePath,
                    methodName,
                    GetNamespaceFromContent(content),
                    GetClassNameForPosition(content, methodIndex),
                    GetLineNumber(content, methodIndex),
                    GetColumn(content, methodIndex)));
            }
        }

        return result;
    }

    private static bool IsGeneratedOrBuildPath(string filePath) {
        string normalized = filePath.Replace('/', Path.DirectorySeparatorChar);
        return normalized.IndexOf(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0 ||
               normalized.IndexOf(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string? GetNamespaceFromContent(string content) {
        Match fileScoped = FileScopedNamespaceRegex.Match(content);
        if (fileScoped.Success) return fileScoped.Groups["ns"].Value;

        Match blockScoped = BlockNamespaceRegex.Match(content);
        return blockScoped.Success ? blockScoped.Groups["ns"].Value : null;
    }

    private static string? GetClassNameForPosition(string content, int position) {
        Match? lastMatch = null;
        foreach (Match match in ClassRegex.Matches(content)) {
            if (match.Index >= position) break;
            lastMatch = match;
        }

        return lastMatch?.Groups["name"].Value;
    }

    private static int GetLineNumber(string content, int position) {
        int line = 0;
        for (int i = 0; i < position && i < content.Length; i++) {
            if (content[i] == '\n') line++;
        }

        return line;
    }

    private static int GetColumn(string content, int position) {
        int pos = Math.Min(position, content.Length);
        int lastNewline = content.LastIndexOf('\n', pos - 1);
        return pos - lastNewline - 1;
    }

    private static int ScoreCandidate(Candidate candidate, string? preferredType, string? fallbackType, string companionFile) {
        int score = 0;

        string? candidateType = candidate.GetFullyQualifiedTypeName();
        if (!string.IsNullOrWhiteSpace(preferredType) && string.Equals(candidateType, preferredType, StringComparison.Ordinal)) {
            score += 100;
        }

        if (!string.IsNullOrWhiteSpace(fallbackType) && string.Equals(candidateType, fallbackType, StringComparison.Ordinal)) {
            score += 50;
        }

        if (string.Equals(candidate.FilePath, companionFile, StringComparison.OrdinalIgnoreCase)) {
            score += 25;
        }

        if (!string.IsNullOrWhiteSpace(preferredType)) {
            string resolvedPreferredType = preferredType;
            if (string.Equals(candidate.ClassName, resolvedPreferredType.Split('.').Last(), StringComparison.Ordinal)) {
                score += 10;
            }
        }

        return score;
    }

    private sealed class Candidate {
        public string FilePath { get; }
        public string MethodName { get; }
        public string? Namespace { get; }
        public string? ClassName { get; }
        public int LineNumber { get; }
        public int Column { get; }

        public Candidate(string filePath, string methodName, string? @namespace, string? className, int lineNumber, int column) {
            FilePath = filePath;
            MethodName = methodName;
            Namespace = @namespace;
            ClassName = className;
            LineNumber = lineNumber;
            Column = column;
        }

        public string? GetFullyQualifiedTypeName() {
            if (string.IsNullOrWhiteSpace(ClassName)) return null;
            return string.IsNullOrWhiteSpace(Namespace) ? ClassName : Namespace + "." + ClassName;
        }
    }

    private sealed class ScoredCandidate {
        public Candidate Candidate { get; }
        public int Score { get; }

        public ScoredCandidate(Candidate candidate, int score) {
            Candidate = candidate;
            Score = score;
        }
    }
}