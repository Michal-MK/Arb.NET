namespace Arb.NET;

public static class NewLineHelper {
    public const string LF = "\n";
    public const string CRLF = "\r\n";
    public const string CR = "\r";

    public static string? FromEndOfLine(string? endOfLine) {
        switch (endOfLine?.Trim().ToLowerInvariant()) {
            case "lf": return LF;
            case "crlf": return CRLF;
            case "cr": return CR;
            default: return null;
        }
    }

    public static string? Detect(string? content) {
        if (string.IsNullOrEmpty(content)) return null;

        int index = content!.IndexOfAny(['\r', '\n']);
        if (index < 0) return null;

        if (content[index] == '\n') return LF;
        return index + 1 < content.Length && content[index + 1] == '\n' ? CRLF : CR;
    }

    public static string Normalize(string text, string? newLine) {
        if (newLine is null) return text;

        return text
            .Replace(CRLF, LF)
            .Replace(CR, LF)
            .Replace(LF, newLine);
    }
}
