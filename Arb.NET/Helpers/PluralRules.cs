using System.Text;

namespace Arb.NET;

/// <summary>
/// CLDR cardinal plural rules https://unicode.org/cldr/charts/latest/supplemental/language_plural_rules.html
/// </summary>
internal static class PluralRules {
    public const string ZERO = "zero";
    public const string ONE = "one";
    public const string TWO = "two";
    public const string FEW = "few";
    public const string MANY = "many";
    public const string OTHER = "other";

    public static readonly IReadOnlyList<string> Categories = [ZERO, ONE, TWO, FEW, MANY, OTHER];

    public static bool IsCategory(string selector) => Categories.Contains(selector, StringComparer.Ordinal);

    private static readonly (string Category, string Condition)[] OTHER_ONLY = [];

    private static readonly (string, string)[] ONE_IS_1 = [(ONE, "n == 1")];

    private static readonly (string, string)[] ONE_IS_0_OR_1 = [(ONE, "n <= 1")];

    private static readonly (string, string)[] FRENCH = [
        (ONE, "n <= 1"),
        (MANY, "n != 0 && n % 1000000 == 0"),
    ];

    private static readonly (string, string)[] SPANISH = [
        (ONE, "n == 1"),
        (MANY, "n != 0 && n % 1000000 == 0"),
    ];

    private static readonly (string, string)[] ICELANDIC = [(ONE, "n % 10 == 1 && n % 100 != 11")];

    private static readonly (string, string)[] FILIPINO = [(ONE, "n % 10 != 4 && n % 10 != 6 && n % 10 != 9")];

    private static readonly (string, string)[] CZECH = [
        (ONE, "n == 1"),
        (FEW, "n >= 2 && n <= 4"),
    ];

    private static readonly (string, string)[] POLISH = [
        (ONE, "n == 1"),
        (FEW, "n % 10 >= 2 && n % 10 <= 4 && (n % 100 < 12 || n % 100 > 14)"),
        (MANY, "true"),
    ];

    private static readonly (string, string)[] RUSSIAN = [
        (ONE, "n % 10 == 1 && n % 100 != 11"),
        (FEW, "n % 10 >= 2 && n % 10 <= 4 && (n % 100 < 12 || n % 100 > 14)"),
        (MANY, "true"),
    ];

    private static readonly (string, string)[] CROATIAN = [
        (ONE, "n % 10 == 1 && n % 100 != 11"),
        (FEW, "n % 10 >= 2 && n % 10 <= 4 && (n % 100 < 12 || n % 100 > 14)"),
    ];

    private static readonly (string, string)[] LITHUANIAN = [
        (ONE, "n % 10 == 1 && (n % 100 < 11 || n % 100 > 19)"),
        (FEW, "n % 10 >= 2 && (n % 100 < 11 || n % 100 > 19)"),
    ];

    private static readonly (string, string)[] LATVIAN = [
        (ZERO, "n % 10 == 0 || (n % 100 >= 11 && n % 100 <= 19)"),
        (ONE, "n % 10 == 1 && n % 100 != 11"),
    ];

    private static readonly (string, string)[] ROMANIAN = [
        (ONE, "n == 1"),
        (FEW, "n == 0 || (n % 100 >= 1 && n % 100 <= 19)"),
    ];

    private static readonly (string, string)[] SLOVENIAN = [
        (ONE, "n % 100 == 1"),
        (TWO, "n % 100 == 2"),
        (FEW, "n % 100 == 3 || n % 100 == 4"),
    ];

    private static readonly (string, string)[] ARABIC = [
        (ZERO, "n == 0"),
        (ONE, "n == 1"),
        (TWO, "n == 2"),
        (FEW, "n % 100 >= 3 && n % 100 <= 10"),
        (MANY, "n % 100 >= 11"),
    ];

    private static readonly (string, string)[] ONE_TWO = [
        (ONE, "n == 1"),
        (TWO, "n == 2"),
    ];

    private static readonly (string, string)[] IRISH = [
        (ONE, "n == 1"),
        (TWO, "n == 2"),
        (FEW, "n >= 3 && n <= 6"),
        (MANY, "n >= 7 && n <= 10"),
    ];

    private static readonly (string, string)[] WELSH = [
        (ZERO, "n == 0"),
        (ONE, "n == 1"),
        (TWO, "n == 2"),
        (FEW, "n == 3"),
        (MANY, "n == 6"),
    ];

    private static readonly (string, string)[] MALTESE = [
        (ONE, "n == 1"),
        (TWO, "n == 2"),
        (FEW, "n == 0 || (n % 100 >= 3 && n % 100 <= 10)"),
        (MANY, "n % 100 >= 11 && n % 100 <= 19"),
    ];

    private static readonly (string, string)[] SCOTTISH_GAELIC = [
        (ONE, "n == 1 || n == 11"),
        (TWO, "n == 2 || n == 12"),
        (FEW, "(n >= 3 && n <= 10) || (n >= 13 && n <= 19)"),
    ];

    private static readonly (string, string)[] BRETON = [
        (ONE, "n % 10 == 1 && n % 100 != 11 && n % 100 != 71 && n % 100 != 91"),
        (TWO, "n % 10 == 2 && n % 100 != 12 && n % 100 != 72 && n % 100 != 92"),
        (FEW, "(n % 10 == 3 || n % 10 == 4 || n % 10 == 9) && (n % 100 < 10 || n % 100 > 19) && (n % 100 < 70 || n % 100 > 79) && (n % 100 < 90 || n % 100 > 99)"),
        (MANY, "n != 0 && n % 1000000 == 0"),
    ];

    private static readonly (string, string)[] MANX = [
        (ONE, "n % 10 == 1"),
        (TWO, "n % 10 == 2"),
        (FEW, "n % 20 == 0"),
    ];

    private static readonly (string, string)[] CENTRAL_ATLAS_TAMAZIGHT = [(ONE, "n <= 1 || (n >= 11 && n <= 99)")];

    private static readonly (string, string)[] TACHELHIT = [
        (ONE, "n <= 1"),
        (FEW, "n >= 2 && n <= 10"),
    ];

    private static readonly (string, string)[] SAMOGITIAN = [
        (ONE, "n % 10 == 1 && n % 100 != 11"),
        (TWO, "n == 2"),
        (FEW, "n % 10 >= 2 && (n % 100 < 11 || n % 100 > 19)"),
    ];

    private static readonly (string, string)[] ZERO_ONE = [
        (ZERO, "n == 0"),
        (ONE, "n == 1"),
    ];

    private static readonly Dictionary<string, (string Category, string Condition)[]> RULES = Build();

    private static Dictionary<string, (string, string)[]> Build() {
        Dictionary<string, (string, string)[]> rules = new(StringComparer.OrdinalIgnoreCase);

        void Add((string, string)[] rule, params string[] locales) {
            foreach (string locale in locales) rules[locale] = rule;
        }

        // Locale lists follow common/supplemental/plurals.xml of unicode-org/cldr (checked 2026-11).
        Add(OTHER_ONLY, "bm", "bo", "dz", "hnj", "id", "ig", "ii", "in", "ja", "jbo", "jv", "jw", "kde", "kea", "km", "ko",
            "lkt", "lo", "ms", "my", "nqo", "osa", "sah", "ses", "sg", "su", "th", "to", "tpi", "wo", "yo", "yue", "zh");
        Add(ONE_IS_1, "af", "an", "asa", "ast", "az", "bal", "bem", "bez", "bg", "brx", "ce", "cgg", "chr", "ckb", "da", "de",
            "dv", "ee", "el", "en", "eo", "et", "eu", "fi", "fo", "fur", "fy", "gsw", "ha", "haw", "hu", "ia", "ie", "io",
            "ji", "jgo",
            "jmc", "ka", "kaj", "kcg", "kk", "kkj", "kl", "ks", "ksb", "ku", "ky", "lb", "lg", "lij", "mas", "mgo", "ml",
            "mn", "mr", "nah", "nb", "nd", "ne", "nl", "nn", "nnh", "no", "nr", "ny", "nyn", "om", "or", "os", "pap", "ps",
            "rm", "rof", "rwk", "saq", "sc", "sd", "sdh", "seh", "sn", "so", "sq", "ss", "ssy", "st", "sv", "sw", "syr",
            "ta", "te", "teo", "tig", "tk", "tn", "tr", "ts", "ug", "ur", "uz", "ve", "vo", "vun", "wae", "xh", "xog",
            "yi");
        Add(ONE_IS_0_OR_1, "ak", "am", "as", "bho", "bn", "csw", "doi", "fa", "ff", "gu", "guw", "hi", "hy", "kab", "kn",
            "kok", "ln", "mg", "nso", "pa", "pcm", "si", "tg", "ti", "vi", "wa", "zu");
        Add(CENTRAL_ATLAS_TAMAZIGHT, "tzm");
        Add(TACHELHIT, "shi");
        Add(FRENCH, "fr", "pt");
        Add(SPANISH, "es", "it", "ca", "gl", "pt_PT", "vec", "lld", "scn");
        Add(ICELANDIC, "is", "mk");
        Add(FILIPINO, "fil", "tl", "ceb");
        Add(SAMOGITIAN, "sgs");
        Add(CZECH, "cs", "sk");
        Add(POLISH, "pl");
        Add(RUSSIAN, "ru", "uk", "be");
        Add(CROATIAN, "hr", "sr", "bs", "sh");
        Add(LITHUANIAN, "lt");
        Add(LATVIAN, "lv", "prg");
        Add(ROMANIAN, "ro", "mo");
        Add(SLOVENIAN, "sl", "dsb", "hsb");
        Add(ARABIC, "ar", "ars");
        Add(ONE_TWO, "he", "iw", "iu", "naq", "sat", "se", "sma", "smi", "smj", "smn", "sms");
        Add(IRISH, "ga");
        Add(WELSH, "cy");
        Add(MALTESE, "mt");
        Add(SCOTTISH_GAELIC, "gd");
        Add(BRETON, "br");
        Add(MANX, "gv");
        Add(ZERO_ONE, "ksh", "lag", "blo", "cv");
        return rules;
    }

    public static IReadOnlyList<(string Category, string Condition)> GetRules(string? locale, out bool isKnown) {
        string normalized = StringHelper.NormalizeLocale(locale);
        if (RULES.TryGetValue(normalized, out (string, string)[]? rule)) {
            isKnown = true;
            return rule;
        }

        int sep = normalized.IndexOf('_');
        string language = sep < 0 ? normalized : normalized.Substring(0, sep);
        if (RULES.TryGetValue(language, out rule)) {
            isKnown = true;
            return rule;
        }

        isKnown = false;
        return ONE_IS_1;
    }

    /// <summary>
    /// Emits the body of <c>private static string {methodName}(int count)</c> returning the CLDR category name.
    /// </summary>
    public static void AppendCategoryMethod(StringBuilder sb, string methodName, string? locale) {
        IReadOnlyList<(string Category, string Condition)> rules = GetRules(locale, out bool isKnown);
        string label = string.IsNullOrWhiteSpace(locale) ? "unknown" : locale!;

        sb.AppendLine();
        sb.AppendLine(isKnown
                          ? $"    // CLDR cardinal plural rules for '{label}' (integer values)."
                          : $"    // No CLDR plural rules known for '{label}', using the 'one' (n = 1) / 'other' rule.");
        sb.AppendLine($"    private static string {methodName}(int count) {{");
        if (rules.Count == 0) {
            sb.AppendLine($"        return \"{OTHER}\";");
        }
        else {
            sb.AppendLine("        long n = count < 0 ? -(long)count : count;");
            foreach ((string category, string condition) in rules) {
                if (condition == "true") {
                    sb.AppendLine($"        return \"{category}\";");
                    break;
                }
                sb.AppendLine($"        if ({condition}) return \"{category}\";");
            }
            if (rules[rules.Count - 1].Condition != "true") {
                sb.AppendLine($"        return \"{OTHER}\";");
            }
        }
        sb.AppendLine("    }");
    }
}
