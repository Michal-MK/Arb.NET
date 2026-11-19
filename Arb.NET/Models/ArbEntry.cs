using System.Text;
using System.Text.RegularExpressions;

namespace Arb.NET;

/// <summary>
/// Represents a single localization entry in an .arb file
/// </summary>
public record ArbEntry {
    private static readonly Regex PluralIntentRegex = new(@"\{(\w+),\s*plural[\s,]", RegexOptions.Compiled);

    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public ArbMetadata? Metadata { get; set; }

    public bool IsParametric(out List<ArbParameterDefinition> defs) => _IsParametricByValue(Value, out defs);

    internal static List<ArbParameterDefinition> ParseParameters(string value) {
        _IsParametricByValue(value, out List<ArbParameterDefinition> defs);
        return defs;
    }

    public IReadOnlyList<string> GetPlaceholderNames() {
        List<string> names = [];
        HashSet<string> seen = new(StringComparer.Ordinal);

        if (IsParametric(out List<ArbParameterDefinition> defs)) {
            foreach (ArbParameterDefinition def in defs) {
                if (seen.Add(def.Name)) {
                    names.Add(def.Name);
                }
            }
        }

        if (Metadata?.Placeholders != null) {
            foreach (string name in Metadata.Placeholders.Keys) {
                if (seen.Add(name)) {
                    names.Add(name);
                }
            }
        }

        return names;
    }

    public bool RenamePlaceholder(string oldName, string newName) {
        if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName)) return false;

        oldName = oldName.Trim();
        newName = newName.Trim();

        if (string.Equals(oldName, newName, StringComparison.Ordinal)) return false;
        if (!StringHelper.IsValidPlaceholderName(newName)) return false;

        bool changed = false;

        string renamedValue = RenamePlaceholderReferences(Value, oldName, newName);
        if (!string.Equals(renamedValue, Value, StringComparison.Ordinal)) {
            Value = renamedValue;
            changed = true;
        }

        if (Metadata?.Placeholders != null && Metadata.Placeholders.ContainsKey(oldName)) {
            Metadata.Placeholders = RenamePlaceholderMetadata(Metadata.Placeholders, oldName, newName);
            changed = true;
        }

        return changed;
    }

    /// <summary>
    /// Detects values that look like plurals but failed strict parsing.
    /// Returns true with a diagnostic message when the intent is clearly plural but syntax is invalid.
    /// </summary>
    public bool TryDetectMangledPlural(out string? diagnostic) {
        diagnostic = null;
        Match match = PluralIntentRegex.Match(Value);
        if (!match.Success) return false;

        // If strict parsing already produced a plural definition, it's not mangled
        if (IsParametric(out List<ArbParameterDefinition> defs) &&
            defs.Exists(d => d is ArbPluralizationParameterDefinition)) {
            return false;
        }

        string name = match.Groups[1].Value;
        int commaIndex = match.Index + 1 + name.Length;
        TryParsePlural(Value, match.Index, commaIndex, 0, out _, out _, out _, out string? reason);

        diagnostic = $"Key '{Key}': Value looks like a plural for '{name}' but the syntax is invalid: " +
                     (reason ?? "unrecognized plural syntax") + ". " +
                     "Example: '{" + name + ", plural, one{...} other{...}}'.";
        return true;
    }

    private const int MAX_NESTING_DEPTH = 8;

    private static bool _IsParametricByValue(string value, out List<ArbParameterDefinition> defs) {
        defs = [];
        ScanParameters(value, defs, 0);
        return defs.Count > 0;
    }


    private static void ScanParameters(string value, List<ArbParameterDefinition> defs, int depth) {
        int index = 0;
        int latestOpenBraceIndex = -1;
        bool isEscaping = false;
        bool? isParameterNumeric = null;

        int maxIterations = value.Length * 2 + 16; // Prevent infinite loops
        int iterations = 0;
        while (index < value.Length) {
            if (iterations++ > maxIterations) {
                break; // Too many iterations, likely malformed input
            }

            char current = value[index];

            if (current == '\\') {
                isEscaping = true;
                index++;
                continue;
            }

            if (current == '{') {
                if (isEscaping) {
                    isEscaping = false;
                    index++;
                    continue;
                }
                latestOpenBraceIndex = index;
                index++;
                continue;
            }

            if (latestOpenBraceIndex != -1) {
                if (StringHelper.IsValidParameterChar(current)) {
                    isParameterNumeric ??= char.IsDigit(current);
                    if (char.IsDigit(current) && isParameterNumeric.Value) {
                        // All-digit parameter so far — still valid (e.g. {0}, {1})
                        index++;
                        continue;
                    }
                    if (char.IsDigit(current) && !isParameterNumeric.Value) {
                        // Letter-started name followed by digit — valid identifier (e.g. {param0} from RESX migration)
                        index++;
                        continue;
                    }
                    if (!StringHelper.IsValidParameterLetter(current) && isParameterNumeric.Value) {
                        // Digit-started name followed by letter — not a valid ARB parameter
                        latestOpenBraceIndex = -1;
                        isParameterNumeric = null;
                        index++;
                    }
                    index++;
                    continue;
                }
                if (current == ',') {
                    if (TryParsePlural(value, latestOpenBraceIndex, index, depth,
                                       out ArbPluralizationParameterDefinition pluralDef,
                                       out int closeIndex,
                                       out List<ArbParameterDefinition> nested,
                                       out _)) {
                        defs.Add(pluralDef);
                        defs.AddRange(nested);
                        index = closeIndex + 1;
                        latestOpenBraceIndex = -1;
                        isParameterNumeric = null;
                        continue;
                    }
                    latestOpenBraceIndex = -1;
                    isParameterNumeric = null;
                    index++;
                    continue;
                }
                if (current == '}') {
                    string paramName = value.Substring(latestOpenBraceIndex + 1, index - latestOpenBraceIndex - 1);
                    defs.Add(new ArbParameterDefinition {
                        Name = paramName,
                        StartIndex = latestOpenBraceIndex,
                        EndIndex = index + 1,
                    });
                    latestOpenBraceIndex = -1;
                    isParameterNumeric = null;
                    continue;
                }

                // Something unexpected, reset state
                latestOpenBraceIndex = -1;
                isParameterNumeric = null;
            }

            index++;
        }
    }

    // {count, plural, =0{No items} one{{count} item} other{{count} items}}
    // ^     ^
    // |     commaIndex
    // openBraceIndex
    private static bool TryParsePlural(string value, int openBraceIndex, int commaIndex, int depth,
                                       out ArbPluralizationParameterDefinition def,
                                       out int closeIndex,
                                       out List<ArbParameterDefinition> nested,
                                       out string? error) {
        const string PLURAL_KEYWORD = "plural";
        def = new ArbPluralizationParameterDefinition {
            StartIndex = openBraceIndex,
            Name = value.Substring(openBraceIndex + 1, commaIndex - openBraceIndex - 1),
        };
        closeIndex = -1;
        nested = [];
        error = null;

        int i = commaIndex + 1;
        SkipWhitespace(value, ref i);
        if (string.CompareOrdinal(value, i, PLURAL_KEYWORD, 0, PLURAL_KEYWORD.Length) != 0) return false;
        i += PLURAL_KEYWORD.Length;
        if (i < value.Length && value[i] != ',' && !char.IsWhiteSpace(value[i])) return false; // e.g. "pluralize"

        SkipWhitespace(value, ref i);
        if (i >= value.Length || value[i] != ',') {
            error = "expected ',' after 'plural'";
            return false;
        }
        i++;

        List<(int Start, string Content)> forms = [];
        HashSet<string> seenSelectors = new(StringComparer.Ordinal);
        while (true) {
            SkipWhitespace(value, ref i);
            if (i >= value.Length) {
                error = "missing the closing '}' of the plural";
                return false;
            }

            char c = value[i];
            if (c == '}') {
                closeIndex = i;
                def.EndIndex = i + 1;
                break;
            }

            string selector;
            int? exactNumber = null;
            if (c == '=') {
                i++;
                if (i >= value.Length || !ReadInteger(value, ref i, out int number)) {
                    error = "expected a number after '='";
                    return false;
                }
                exactNumber = number;
                selector = "=" + number;
            }
            else if (char.IsDigit(c)) {
                int digitsStart = i;
                while (i < value.Length && char.IsDigit(value[i])) i++;
                string digits = value.Substring(digitsStart, i - digitsStart);
                error = i < value.Length && value[i] == '='
                    ? $"'{digits}=' is not valid, exact matches are written as '={digits}{{...}}'"
                    : $"unexpected '{digits}', exact matches are written as '={digits}{{...}}'";
                return false;
            }
            else if (char.IsLetter(c)) {
                int wordStart = i;
                while (i < value.Length && (char.IsLetterOrDigit(value[i]) || value[i] == '_')) i++;
                selector = value.Substring(wordStart, i - wordStart);
                if (selector == "offset" && i < value.Length && value[i] == ':') {
                    error = "'offset:' is not supported";
                    return false;
                }
                if (!PluralRules.IsCategory(selector)) {
                    error = $"'{selector}' is not a plural category, use '=N' or one of: " +
                            string.Join(", ", PluralRules.Categories);
                    return false;
                }
            }
            else if (c == ',') {
                error = "plural forms must be separated by spaces, not commas";
                return false;
            }
            else {
                error = $"unexpected '{c}' where a plural form ('=N{{...}}' or a category such as 'one{{...}}') was expected";
                return false;
            }

            SkipWhitespace(value, ref i);
            if (i >= value.Length || value[i] != '{') {
                error = $"expected '{{' after '{selector}'";
                return false;
            }

            int contentStart = i + 1;
            if (!ReadContent(value, ref i, out string content)) {
                error = $"form '{selector}' is missing its closing '}}'";
                return false;
            }

            if (!seenSelectors.Add(selector)) {
                error = $"form '{selector}' is defined more than once";
                return false;
            }

            if (exactNumber.HasValue) {
                def.CountableParameters.Add(exactNumber.Value, content);
            }
            else if (selector == PluralRules.OTHER) {
                def.OtherParameter = content;
            }
            else {
                def.CategoryParameters.Add(selector, content);
            }
            forms.Add((contentStart, content));
        }

        // Placeholders used inside the forms become parameters too, e.g. "=1{One item, owner {name}}".
        // References to the plural variable itself are not added, it is already a parameter.
        if (depth < MAX_NESTING_DEPTH) {
            foreach ((int start, string content) in forms) {
                List<ArbParameterDefinition> inner = [];
                ScanParameters(content, inner, depth + 1);
                foreach (ArbParameterDefinition innerDef in inner) {
                    if (innerDef is not ArbPluralizationParameterDefinition && innerDef.Name == def.Name) continue;
                    nested.Add(innerDef with {
                        StartIndex = innerDef.StartIndex + start,
                        EndIndex = innerDef.EndIndex + start,
                    });
                }
            }
        }

        return true;
    }

    private static void SkipWhitespace(string value, ref int index) {
        while (index < value.Length && char.IsWhiteSpace(value[index])) index++;
    }

    private static bool ReadInteger(string value, ref int index, out int number) {
        number = 0;

        if (!char.IsDigit(value[index])) {
            return false;
        }

        while (index < value.Length && char.IsDigit(value[index])) {
            number = number * 10 + (value[index] - '0');
            index++;
        }
        return true;
    }

    private static bool ReadContent(string value, ref int index, out string content) {
        content = string.Empty;
        if (value[index] != '{') {
            return false;
        }

        int braceDepth = 0;
        int contentStartIndex = index + 1;
        index++; // skip initial '{'
        while (index < value.Length) {
            if (value[index] == '\\') {
                index += 2; // skip escaped character
                continue;
            }
            if (value[index] == '{') {
                braceDepth++;
            }
            else if (value[index] == '}') {
                if (braceDepth == 0) {
                    content = value.Substring(contentStartIndex, index - contentStartIndex);
                    index++; // skip closing '}'
                    return true;
                }
                braceDepth--;
            }
            index++;
        }
        return false; // No matching closing brace found
    }

    private static string RenamePlaceholderReferences(string value, string oldName, string newName) {
        if (string.IsNullOrEmpty(value)) return value;

        StringBuilder builder = new(value.Length + Math.Max(0, newName.Length - oldName.Length));
        int copyStart = 0;

        for (int index = 0; index < value.Length; index++) {
            char current = value[index];
            if (current == '\\') {
                index++;
                continue;
            }

            if (current != '{') {
                continue;
            }

            int nameStart = index + 1;
            if (nameStart >= value.Length || !StringHelper.IsValidParameterChar(value[nameStart])) {
                continue;
            }

            int cursor = nameStart + 1;
            while (cursor < value.Length && StringHelper.IsValidParameterChar(value[cursor])) {
                cursor++;
            }

            int nameLength = cursor - nameStart;
            if (nameLength != oldName.Length) {
                continue;
            }

            if (!value.AsSpan(nameStart, nameLength).SequenceEqual(oldName.AsSpan())) {
                continue;
            }

            if (cursor >= value.Length || value[cursor] is not ('}' or ',')) {
                continue;
            }

            builder.Append(value, copyStart, nameStart - copyStart);
            builder.Append(newName);
            copyStart = cursor;
        }

        if (copyStart == 0) {
            return value;
        }

        builder.Append(value, copyStart, value.Length - copyStart);
        return builder.ToString();
    }

    private static Dictionary<string, ArbPlaceholder> RenamePlaceholderMetadata(
        Dictionary<string, ArbPlaceholder> placeholders,
        string oldName,
        string newName) {
        Dictionary<string, ArbPlaceholder> renamed = new(placeholders.Count, StringComparer.Ordinal);
        foreach (KeyValuePair<string, ArbPlaceholder> placeholder in placeholders) {
            string key = string.Equals(placeholder.Key, oldName, StringComparison.Ordinal)
                ? newName
                : placeholder.Key;
            renamed[key] = placeholder.Value;
        }

        return renamed;
    }
}