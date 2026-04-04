namespace Arb.NET.IDE.Common.Services;

public static class ArbTranslationLocaleService {

    public static List<string> GetVisibleTargetLocales(
        IEnumerable<string> locales,
        string? sourceLocale,
        bool includeSubcultures) {
        string normalizedSource = StringHelper.NormalizeLocale(sourceLocale);

        return locales
            .Where(locale => {
                string normalizedLocale = StringHelper.NormalizeLocale(locale);
                if (string.Equals(normalizedLocale, normalizedSource, StringComparison.OrdinalIgnoreCase)) {
                    return false;
                }

                return includeSubcultures || !StringHelper.IsSubcultureLocale(normalizedLocale);
            })
            .ToList();
    }
}