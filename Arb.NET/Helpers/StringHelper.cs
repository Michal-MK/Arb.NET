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

    public static string NormalizeLocale(string? locale) {
        return string.IsNullOrWhiteSpace(locale)
            ? string.Empty
            : locale!.Replace("-", "_").Replace(" ", string.Empty);
    }

    public static string JsonString(string value) {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    public static string XmlEscape(string value) {
        return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }

    public static bool IsTrue(string? value) {
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }
}