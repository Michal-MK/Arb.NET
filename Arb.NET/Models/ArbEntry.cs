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
        diagnostic = $"Key '{Key}': Value looks like a plural for '{name}' but the syntax is invalid. " +
                     "Ensure forms are separated by spaces (not commas) and use '=N{{...}}' syntax (not 'N={{...}}').";
        return true;
    }

    private bool _IsParametricByValue(string value, out List<ArbParameterDefinition> defs) {
        defs = [];
        int index = 0;
        int latestOpenBraceIndex = -1;
        bool isEscaping = false;
        bool? isParameterNumeric = null;

        int maxIterations = 1000; // Prevent infinite loops
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
                    if (HandlePluralizationParameter(value, latestOpenBraceIndex, ref index, out ArbPluralizationParameterDefinition? pluralDef)) {
                        defs.Add(pluralDef);
                        index++;
                        continue;
                    }
                    latestOpenBraceIndex = -1;
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
        return defs.Count > 0;
    }

    // {count, plural, =0{No items} =1{{count} item} other{{count} items}}
    //       ^
    // Here we are
    private bool HandlePluralizationParameter(string value, int start, ref int index, out ArbPluralizationParameterDefinition def) {
        const string PLURAL_INDICATOR = ", plural, ";
        def = new ArbPluralizationParameterDefinition {
            StartIndex = start,
            Name = value.Substring(start + 1, index - 1 - start),
        };
        if (!value.Substring(index).StartsWith(PLURAL_INDICATOR)) {
            return false;
        }
        index += PLURAL_INDICATOR.Length;

        // {count, plural, =0{No items} =1{{count} item} other{{count} items}}
        //                 ^
        // Here we are

        bool foundClosingBrace = false;
        int maxIterations = 1000; // Prevent infinite loops
        int iterations = 0;
        while (!foundClosingBrace) {
            if (iterations++ > maxIterations) {
                return false; // Too many iterations, likely malformed input
            }

            // expect '=' or 'other{'
            if (value[index] == '=') {
                // {count, plural, =0{No items} =1{{count} item} other{{count} items}}
                //                 ^
                // Here we are
                index += 1; // skip '='
                bool success1 = ReadInteger(value, ref index, out int number);
                if (!success1) {
                    return false;
                }

                // {count, plural, =0{No items} =1{{count} item} other{{count} items}}
                //                   ^
                // Here we are

                bool success2 = ReadContent(value, ref index, out string content);

                // {count, plural, =0{No items} =1{{count} item} other{{count} items}}
                //                             ^                ^
                // Here we are
                if (!success2) {
                    return false;
                }

                def.CountableParameters.Add(number, content);

                index++;

                // {count, plural, =0{No items} =1{{count} item} other{{count} items}}
                //                              ^                ^
                // Here we are
            }
            else if (value[index] == 'o') {
                // {count, plural, =0{No items} =1{One item} other{{count} items}}
                //                                           ^
                // Here we are
                const string OTHER_INDICATOR = "other";
                if (!value.Substring(index).StartsWith(OTHER_INDICATOR)) {
                    return false;
                }
                index += OTHER_INDICATOR.Length;
                bool success = ReadContent(value, ref index, out string content);
                if (!success) {
                    return false;
                }

                // {count, plural, =0{No items} =1{One item} other{{count} items}}
                //                                                               ^
                // Here we are

                def.OtherParameter = content;
            }
            if (value[index] == '}') {
                foundClosingBrace = true;
                def.EndIndex = index + 1;
            }
        }
        return true;
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