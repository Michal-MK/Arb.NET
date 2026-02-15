namespace Arb.NET;

internal static class StringHelper {

    public static bool IsValidParameterChar(char c) {
        return IsValidParameterLetter(c) || c is >= '0' and <= '9';
    }
    
    public static string ToPascalCase(string key) {
        if (string.IsNullOrEmpty(key)) return key;
        return char.ToUpperInvariant(key[0]) + key.Substring(1);
    }

    public static string EscapeString(string value) {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    public static bool IsValidParameterLetter(char c) {
        return c is >= 'a' and <= 'z' || c is >= 'A' and <= 'Z' || c == '_';
    }
}