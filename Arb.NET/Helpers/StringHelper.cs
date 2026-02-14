namespace Arb.NET;

internal static class StringHelper {
    public static string ToPascalCase(string key) {
        if (string.IsNullOrEmpty(key)) return key;
        return char.ToUpperInvariant(key[0]) + key.Substring(1);
    }

    public static string EscapeString(string value) {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}