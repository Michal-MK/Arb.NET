using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using Arb.NET.Translation.Models;

namespace Arb.NET.Translation;

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

    /// <summary>
    /// Maps an ARB locale code to a Google Translate language code, without relying on
    /// CultureInfo/ICU - this runs inside Arb.NET.Wasm under InvariantGlobalization, where
    /// CultureInfo throws for any non-invariant culture. Locale codes seen in .arb files are
    /// simple BCP-47-ish tags (e.g. "en", "en_US", "zh_TW"), so a plain split on the
    /// language/region separator is sufficient - full ICU culture-name normalization was
    /// never actually exercised by the special cases below anyway.
    /// </summary>
    private static string GoogleLangCode(string locale) {
        string normalized = locale.Replace('_', '-');
        string[] parts = normalized.Split('-');
        string iso1 = parts[0].ToLowerInvariant();
        string? region = parts.Length > 1 ? parts[1].ToLowerInvariant() : null;

        if (iso1 == "zh") {
            string[] twRegions = ["hant", "cht", "hk", "mo", "tw"];
            return region != null && twRegions.Contains(region) ? "zh-TW" : "zh-CN";
        }

        if (iso1 == "haw") {
            return "haw";
        }

        return iso1;
    }
}