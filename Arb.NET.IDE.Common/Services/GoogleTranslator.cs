using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using Arb.NET.IDE.Common.Models;

namespace Arb.NET.IDE.Common.Services;

public sealed class GoogleTranslator : ITranslator {
    private const string BASE_URL = "https://translate.googleapis.com/translate_a/single";

    public (bool Valid, string? Error) ValidateSettings() => (true, null);

    public async Task<IReadOnlyList<string>> TranslateBatchAsync(
        string sourceLocale,
        string targetLocale,
        IReadOnlyList<AzureTranslationItem> items,
        CancellationToken cancellationToken) {

        string from = GoogleLangCode(sourceLocale);
        string to = GoogleLangCode(targetLocale);
        List<string> results = new(items.Count);

        using HttpClient client = new();

        foreach (AzureTranslationItem item in items) {
            cancellationToken.ThrowIfCancellationRequested();
            string translated = await TranslateOneAsync(client, item.SourceText, from, to, cancellationToken).ConfigureAwait(false);
            results.Add(translated);
        }

        return results;
    }

    private static async Task<string> TranslateOneAsync(HttpClient client, string text, string from, string to, CancellationToken ct) {
        string url = BuildUrl(from, to, text);
        HttpResponseMessage response = await client.GetAsync(new Uri(url), ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return ParseResponse(body);
    }

    private static string BuildUrl(string from, string to, string text) {
        string q = WebUtility.UrlEncode(text)!;
        return $"{BASE_URL}?client=gtx&dt=t&sl={from}&tl={to}&q={q}";
    }

    private static string ParseResponse(string json) {
        JsonNode? node = JsonNode.Parse(json);
        if (node is JsonArray level1 && level1.FirstOrDefault() is JsonArray level2) {
            return string.Concat(level2.OfType<JsonArray>().Select(item => item.FirstOrDefault()));
        }
        return string.Empty;
    }

    private static string GoogleLangCode(string locale) {
        // Use the two-letter ISO language name, with special handling for Chinese.
        CultureInfo culture;
        try {
            culture = new CultureInfo(locale);
        }
        catch {
            // Fallback: use locale as-is if it is not a recognised culture.
            return locale;
        }

        string iso1 = culture.TwoLetterISOLanguageName;
        string name = culture.Name;

        string[] twCultures = ["zh-hant", "zh-cht", "zh-hk", "zh-mo", "zh-tw"];
        if (string.Equals(iso1, "zh", StringComparison.OrdinalIgnoreCase)) {
            return twCultures.Contains(name, StringComparer.OrdinalIgnoreCase) ? "zh-TW" : "zh-CN";
        }

        if (string.Equals(name, "haw-us", StringComparison.OrdinalIgnoreCase)) {
            return "haw";
        }

        return iso1;
    }
}