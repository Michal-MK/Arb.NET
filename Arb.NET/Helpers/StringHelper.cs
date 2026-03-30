namespace Arb.NET;

public static class StringHelper {
    public static bool IsValidParameterChar(char c) {
        return IsValidParameterLetter(c) ||
               c is >= '0' and <= '9';
    }

    public static bool IsValidParameterLetter(char c) {
        return c is >= 'a' and <= 'z' ||
               c is >= 'A' and <= 'Z' ||
               c == '_';
    }

    public static string ToPascalCase(string key) {
        if (string.IsNullOrEmpty(key)) return key;
        return char.ToUpperInvariant(key[0]) + key.Substring(1);
    }

    public static string ToCamelCase(string key) {
        if (string.IsNullOrEmpty(key)) return key;
        return char.ToLowerInvariant(key[0]) + key.Substring(1);
    }

    public static string? FirstNonEmpty(params string?[] values) {
        return values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
    }

    public static string? InferLangCodeFromFilename(string fileNameWithoutExt) {
        string[] segments = fileNameWithoutExt.Split('_');
        return segments.Length <= 1
            ? null
            : string.Join("_", segments, 1, segments.Length - 1);
    }

    public static string NormalizeLocale(string? locale) {
        return string.IsNullOrWhiteSpace(locale)
            ? string.Empty
            : locale!.Replace("-", "_").Replace(" ", string.Empty);
    }

    public static string JsonString(string value) {
        return value.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n")
                    .Replace("\r", "\\r")
                    .Replace("\t", "\\t");
    }
    
    public static string ArbKeyForEnumMember(string enumSimpleName, string memberName) {
        return ToCamelCase(enumSimpleName) + ToPascalCase(memberName);
    }

    public static string XmlEscape(string value) {
        return value.Replace("&", "&amp;")
                    .Replace("<", "&lt;")
                    .Replace(">", "&gt;")
                    .Replace("\r\n", "&#10;")
                    .Replace("\r", "&#10;")
                    .Replace("\n", "&#10;");
    }
}