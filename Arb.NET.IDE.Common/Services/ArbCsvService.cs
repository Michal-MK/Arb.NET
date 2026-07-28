using Arb.NET.Models;

namespace Arb.NET.IDE.Common.Services;

public static class ArbCsvService {
    public static CsvImportPreview BuildImportPreview(string arbDirectory, string csvContent) {
        if (string.IsNullOrWhiteSpace(arbDirectory)) {
            throw new ArgumentException("ARB directory is required.", nameof(arbDirectory));
        }

        string? defaultLocale = LocalizationYamlService.ResolveDefaultLocaleForArbDirectory(arbDirectory);
        return ArbCsvCore.BuildImportPreview(LoadLocaleContents(arbDirectory), csvContent, defaultLocale);
    }

    public static CsvImportApplyResult ApplyImport(
        string arbDirectory,
        string csvContent,
        IReadOnlyList<string> mappings,
        CsvImportMode mode
    ) {
        if (string.IsNullOrWhiteSpace(arbDirectory)) {
            throw new ArgumentException("ARB directory is required.", nameof(arbDirectory));
        }

        (CsvImportApplyResult result, Dictionary<string, string> updatedContents) =
            ArbCsvCore.ApplyImport(LoadLocaleContents(arbDirectory), csvContent, mappings, mode);

        foreach (KeyValuePair<string, string> kvp in updatedContents) {
            string filePath = Path.Combine(arbDirectory, kvp.Key + Constants.ARB_FILE_EXT);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, kvp.Value, Constants.UTF8_NO_BOM);
        }

        return result;
    }

    public static string Export(string arbDirectory) {
        if (string.IsNullOrWhiteSpace(arbDirectory)) {
            throw new ArgumentException("ARB directory is required.", nameof(arbDirectory));
        }

        return ArbCsvCore.Export(LoadLocaleContents(arbDirectory));
    }

    private static Dictionary<string, string> LoadLocaleContents(string arbDirectory) {
        if (!Directory.Exists(arbDirectory)) {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        Dictionary<string, string> contents = new(StringComparer.OrdinalIgnoreCase);
        IOrderedEnumerable<string> dirs = Directory.EnumerateFiles(arbDirectory, Constants.ANY_ARB)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
        foreach (string filePath in dirs) {
            string localeHint = Path.GetFileNameWithoutExtension(filePath);
            contents[localeHint] = File.ReadAllText(filePath);
        }

        return contents;
    }
}