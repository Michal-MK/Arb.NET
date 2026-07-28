using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using Arb.NET.Models;
using Arb.NET.Translation;
using Arb.NET.Translation.Models;

namespace Arb.NET.Wasm;

public static partial class ArbInterop {
    private static readonly ArbParser PARSER = new();

    [JSExport]
    public static string ParseContent(string content) {
        ArbParseResult result = PARSER.ParseContent(content);
        return JsonSerializer.Serialize(result, ArbJsonContext.Default.ArbParseResult);
    }

    [JSExport]
    public static string Serialize(string documentJson) {
        ArbDocument document = JsonSerializer.Deserialize(documentJson, ArbJsonContext.Default.ArbDocument)
            ?? throw new ArgumentException("Could not deserialize ArbDocument JSON.", nameof(documentJson));
        return ArbSerializer.Serialize(document);
    }

    /// <param name="localeContentsJson">JSON object: locale (filename hint) -> raw .arb content.</param>
    [JSExport]
    public static string BuildCsvImportPreview(string localeContentsJson, string csvContent, string? defaultLocale) {
        Dictionary<string, string> localeContents = DeserializeLocaleContents(localeContentsJson);
        CsvImportPreview preview = ArbCsvCore.BuildImportPreview(localeContents, csvContent, defaultLocale);
        return JsonSerializer.Serialize(preview, ArbJsonContext.Default.CsvImportPreview);
    }

    /// <param name="localeContentsJson">JSON object: locale (filename hint) -> raw .arb content.</param>
    /// <param name="mappingsJson">JSON array of per-CSV-column mappings ("key" / locale / "").</param>
    /// <param name="mode">"merge" or "replaceAll".</param>
    [JSExport]
    public static string ApplyCsvImport(string localeContentsJson, string csvContent, string mappingsJson, string mode) {
        Dictionary<string, string> localeContents = DeserializeLocaleContents(localeContentsJson);
        List<string> mappings = JsonSerializer.Deserialize(mappingsJson, ArbJsonContext.Default.ListString)
            ?? throw new ArgumentException("Could not deserialize mappings JSON.", nameof(mappingsJson));

        CsvImportMode importMode = string.Equals(mode, "replaceAll", StringComparison.OrdinalIgnoreCase)
            ? CsvImportMode.ReplaceAll
            : CsvImportMode.Merge;

        (CsvImportApplyResult result, Dictionary<string, string> updatedLocaleContents) =
            ArbCsvCore.ApplyImport(localeContents, csvContent, mappings, importMode);

        CsvApplyImportResponse response = new() {
            Result = result,
            UpdatedLocaleContents = updatedLocaleContents
        };
        return JsonSerializer.Serialize(response, ArbJsonContext.Default.CsvApplyImportResponse);
    }

    /// <param name="localeContentsJson">JSON object: locale (filename hint) -> raw .arb content.</param>
    [JSExport]
    public static string ExportCsv(string localeContentsJson) {
        Dictionary<string, string> localeContents = DeserializeLocaleContents(localeContentsJson);
        return ArbCsvCore.Export(localeContents);
    }

    /// <param name="provider">"google" or "azureOpenAI".</param>
    /// <param name="settingsJson">AzureTranslationSettings JSON - ignored when provider is "google".</param>
    /// <param name="itemsJson">JSON array of {key, sourceText, description?}.</param>
    /// Returns a JSON array of translated strings, same order/count as itemsJson, or throws
    /// (surfaced to JS as a rejected promise) with the translator's own error message.
    [JSExport]
    public static async Task<string> TranslateBatch(string provider, string settingsJson, string sourceLocale, string targetLocale, string itemsJson) {
        ITranslator translator = CreateTranslator(provider, settingsJson);

        (bool valid, string? error) = translator.ValidateSettings();
        if (!valid) {
            throw new InvalidOperationException(error ?? "Translation settings are invalid.");
        }

        List<AzureTranslationItem> items = JsonSerializer.Deserialize(itemsJson, ArbJsonContext.Default.ListAzureTranslationItem)
            ?? throw new ArgumentException("Could not deserialize items JSON.", nameof(itemsJson));

        IReadOnlyList<string> translations = await translator.TranslateBatchAsync(sourceLocale, targetLocale, items, CancellationToken.None);
        return JsonSerializer.Serialize(translations, ArbJsonContext.Default.IReadOnlyListString);
    }

    /// <param name="provider">"google" or "azureOpenAI".</param>
    /// <param name="settingsJson">AzureTranslationSettings JSON - ignored when provider is "google".</param>
    [JSExport]
    public static string ValidateTranslationSettings(string provider, string settingsJson) {
        ITranslator translator = CreateTranslator(provider, settingsJson);
        (bool valid, string? error) = translator.ValidateSettings();
        return JsonSerializer.Serialize(new ValidateSettingsResponse { Valid = valid, Error = error }, ArbJsonContext.Default.ValidateSettingsResponse);
    }

    private static ITranslator CreateTranslator(string provider, string settingsJson) {
        if (string.Equals(provider, "google", StringComparison.OrdinalIgnoreCase)) {
            return new GoogleTranslator();
        }

        AzureTranslationSettings settings = JsonSerializer.Deserialize(settingsJson, ArbJsonContext.Default.AzureTranslationSettings)
            ?? throw new ArgumentException("Could not deserialize settings JSON.", nameof(settingsJson));
        return new AzureOpenAITranslator(settings);
    }

    private static Dictionary<string, string> DeserializeLocaleContents(string localeContentsJson) {
        return JsonSerializer.Deserialize(localeContentsJson, ArbJsonContext.Default.DictionaryStringString)
            ?? throw new ArgumentException("Could not deserialize locale contents JSON.", nameof(localeContentsJson));
    }
}

public sealed class CsvApplyImportResponse {
    public required CsvImportApplyResult Result { get; init; }
    public required Dictionary<string, string> UpdatedLocaleContents { get; init; }
}

public sealed class ValidateSettingsResponse {
    public required bool Valid { get; init; }
    public string? Error { get; init; }
}