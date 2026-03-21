using Arb.NET.IDE.Common.Models;
using Arb.NET.Models;
using System.Text;

namespace Arb.NET.IDE.Common.Services;

public static class ArbCsvService {
    private const string KEY_MAPPING = "key";

    public static CsvImportPreview BuildImportPreview(string arbDirectory, string csvContent) {
        if (string.IsNullOrWhiteSpace(arbDirectory)) {
            throw new ArgumentException("ARB directory is required.", nameof(arbDirectory));
        }

        List<List<string>> parsedRows = ParseCsv(csvContent);
        if (parsedRows.Count == 0) {
            throw new InvalidOperationException("CSV file is empty.");
        }

        NormalizeRowWidths(parsedRows);

        List<ArbLocaleDocument> localeDocs = LoadLocaleDocuments(arbDirectory);
        List<string> existingLocales = localeDocs
            .Select(doc => doc.Locale)
            .OrderBy(locale => locale, StringComparer.OrdinalIgnoreCase)
            .ToList();

        string? defaultLocale = LocalizationYamlService.ResolveDefaultLocaleForArbDirectory(arbDirectory);
        List<string> headers = parsedRows[0];

        CsvImportPreview preview = new() {
            DefaultLocale = defaultLocale
        };
        preview.Headers.AddRange(headers);

        HashSet<string> availableLocales = new(existingLocales, StringComparer.OrdinalIgnoreCase);

        foreach (string header in headers) {
            string suggested = GuessMapping(header, defaultLocale, existingLocales);
            preview.SuggestedMappings.Add(suggested);

            if (IsLocaleMapping(suggested)) {
                availableLocales.Add(suggested);
            }

            string? guessedLocale = GuessLocaleName(header, existingLocales);
            if (!string.IsNullOrWhiteSpace(guessedLocale)) {
                availableLocales.Add(guessedLocale!);
            }
        }

        if (!string.IsNullOrWhiteSpace(defaultLocale)) {
            availableLocales.Add(defaultLocale!);
        }

        preview.AvailableLocaleMappings.AddRange(availableLocales
            .OrderBy(locale => locale, StringComparer.OrdinalIgnoreCase));

        foreach (List<string> row in parsedRows.Skip(1)) {
            CsvImportPreviewRow previewRow = new();
            previewRow.Cells.AddRange(row);
            preview.Rows.Add(previewRow);
        }

        return preview;
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

        List<List<string>> parsedRows = ParseCsv(csvContent);
        if (parsedRows.Count == 0) {
            throw new InvalidOperationException("CSV file is empty.");
        }

        NormalizeRowWidths(parsedRows);

        CsvImportPayload payload = BuildPayload(parsedRows, mappings);
        List<ArbLocaleDocument> localeDocs = LoadLocaleDocuments(arbDirectory);
        Dictionary<string, ArbLocaleDocument> docsByLocale = localeDocs.ToDictionary(doc => doc.Locale, StringComparer.OrdinalIgnoreCase);

        int createdLocaleCount = 0;
        foreach (string locale in payload.Locales) {
            string normalizedLocale = NormalizeLocale(locale);
            if (docsByLocale.ContainsKey(normalizedLocale)) {
                continue;
            }

            ArbLocaleDocument newDoc = new(
                normalizedLocale,
                Path.Combine(arbDirectory, normalizedLocale + Constants.ARB_FILE_EXT),
                new ArbDocument {
                    Locale = normalizedLocale
                });
            docsByLocale[normalizedLocale] = newDoc;
            createdLocaleCount++;
        }

        switch (mode) {
            case CsvImportMode.Merge:
                ApplyMerge(payload, docsByLocale.Values);
                break;
            case CsvImportMode.ReplaceAll:
                ApplyReplaceAll(payload, docsByLocale.Values);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported CSV import mode.");
        }

        foreach (ArbLocaleDocument doc in docsByLocale.Values) {
            doc.Document.Locale = doc.Locale;
            Directory.CreateDirectory(Path.GetDirectoryName(doc.FilePath)!);
            File.WriteAllText(doc.FilePath, ArbSerializer.Serialize(doc.Document), Constants.UTF8_NO_BOM);
        }

        return new CsvImportApplyResult {
            ImportedKeyCount = payload.OrderedKeys.Count,
            AffectedLocaleCount = docsByLocale.Count,
            CreatedLocaleCount = createdLocaleCount
        };
    }

    public static string Export(string arbDirectory) {
        if (string.IsNullOrWhiteSpace(arbDirectory)) {
            throw new ArgumentException("ARB directory is required.", nameof(arbDirectory));
        }

        List<ArbLocaleDocument> localeDocs = LoadLocaleDocuments(arbDirectory);
        List<string> locales = localeDocs
            .Select(doc => doc.Locale)
            .OrderBy(locale => locale, StringComparer.OrdinalIgnoreCase)
            .ToList();

        SortedSet<string> allKeys = new(StringComparer.Ordinal);
        Dictionary<string, Dictionary<string, string>> valuesByLocale = new(StringComparer.OrdinalIgnoreCase);

        foreach (ArbLocaleDocument doc in localeDocs) {
            Dictionary<string, string> values = new(StringComparer.Ordinal);
            foreach (KeyValuePair<string, ArbEntry> entry in doc.Document.Entries) {
                if (entry.Key.StartsWith("@", StringComparison.Ordinal)) {
                    continue;
                }

                allKeys.Add(entry.Key);
                values[entry.Key] = entry.Value.Value;
            }

            valuesByLocale[doc.Locale] = values;
        }

        StringBuilder builder = new();
        WriteCsvRow(builder, ["key", ..locales]);

        foreach (string key in allKeys) {
            List<string> cells = [key];
            foreach (string locale in locales) {
                string value = valuesByLocale.TryGetValue(locale, out Dictionary<string, string>? localeValues)
                    && localeValues.TryGetValue(key, out string? localizedValue)
                    ? localizedValue
                    : string.Empty;
                cells.Add(value);
            }
            WriteCsvRow(builder, cells);
        }

        return builder.ToString();
    }

    private static void ApplyMerge(CsvImportPayload payload, IEnumerable<ArbLocaleDocument> docs) {
        List<ArbLocaleDocument> docList = docs.ToList();

        foreach (string key in payload.OrderedKeys) {
            foreach (ArbLocaleDocument doc in docList) {
                if (!doc.Document.Entries.ContainsKey(key)) {
                    doc.Document.Entries[key] = new ArbEntry {
                        Key = key,
                        Value = string.Empty
                    };
                }
            }

            foreach (KeyValuePair<string, string> localeValue in payload.ValuesByKey[key]) {
                ArbLocaleDocument doc = docList.First(document => string.Equals(document.Locale, localeValue.Key, StringComparison.OrdinalIgnoreCase));
                doc.Document.Entries[key].Value = localeValue.Value;
            }
        }
    }

    private static void ApplyReplaceAll(CsvImportPayload payload, IEnumerable<ArbLocaleDocument> docs) {
        foreach (ArbLocaleDocument doc in docs) {
            Dictionary<string, ArbEntry> newEntries = [];

            foreach (string key in payload.OrderedKeys) {
                bool hasExistingEntry = doc.Document.Entries.TryGetValue(key, out ArbEntry? existingEntry);
                string value = payload.ValuesByKey[key].TryGetValue(doc.Locale, out string? importedValue)
                    ? importedValue
                    : hasExistingEntry
                        ? existingEntry!.Value
                        : string.Empty;

                ArbEntry nextEntry = hasExistingEntry
                    ? existingEntry! with {
                        Key = key,
                        Value = value
                    }
                    : new ArbEntry {
                        Key = key,
                        Value = value
                    };

                newEntries[key] = nextEntry;
            }

            doc.Document.Entries = newEntries;
        }
    }

    private static CsvImportPayload BuildPayload(List<List<string>> parsedRows, IReadOnlyList<string> mappings) {
        List<string> headers = parsedRows[0];
        List<string> normalizedMappings = NormalizeMappings(headers.Count, mappings);

        int keyColumnIndex = -1;
        HashSet<string> seenLocales = new(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < normalizedMappings.Count; index++) {
            string mapping = normalizedMappings[index];
            if (string.IsNullOrEmpty(mapping)) {
                continue;
            }

            if (string.Equals(mapping, KEY_MAPPING, StringComparison.OrdinalIgnoreCase)) {
                if (keyColumnIndex >= 0) {
                    throw new InvalidOperationException("CSV import mapping must contain exactly one key column.");
                }

                keyColumnIndex = index;
                continue;
            }

            if (!seenLocales.Add(mapping)) {
                throw new InvalidOperationException($"Locale '{mapping}' is mapped more than once.");
            }
        }

        if (keyColumnIndex < 0) {
            throw new InvalidOperationException("CSV import mapping must contain a key column.");
        }

        List<string> locales = normalizedMappings
            .Where(IsLocaleMapping)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        CsvImportPayload payload = new(locales);

        foreach (List<string> row in parsedRows.Skip(1)) {
            string key = row[keyColumnIndex].Trim();
            if (string.IsNullOrWhiteSpace(key)) {
                continue;
            }

            if (!payload.ValuesByKey.ContainsKey(key)) {
                payload.OrderedKeys.Add(key);
                payload.ValuesByKey[key] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            Dictionary<string, string> valuesByLocale = payload.ValuesByKey[key];
            for (int index = 0; index < normalizedMappings.Count; index++) {
                string mapping = normalizedMappings[index];
                if (!IsLocaleMapping(mapping)) {
                    continue;
                }

                valuesByLocale[mapping] = row[index];
            }
        }

        return payload;
    }

    private static List<string> NormalizeMappings(int headerCount, IReadOnlyList<string> mappings) {
        List<string> normalized = [];
        for (int index = 0; index < headerCount; index++) {
            string raw = index < mappings.Count ? mappings[index] : string.Empty;
            string trimmed = raw?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmed)) {
                normalized.Add(string.Empty);
            }
            else if (string.Equals(trimmed, KEY_MAPPING, StringComparison.OrdinalIgnoreCase)) {
                normalized.Add(KEY_MAPPING);
            }
            else {
                normalized.Add(NormalizeLocale(trimmed));
            }
        }

        return normalized;
    }

    private static List<ArbLocaleDocument> LoadLocaleDocuments(string arbDirectory) {
        if (!Directory.Exists(arbDirectory)) {
            return [];
        }

        ArbParser parser = new();
        List<ArbLocaleDocument> result = [];

        foreach (string filePath in Directory.EnumerateFiles(arbDirectory, Constants.ANY_ARB)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)) {
            string content = File.ReadAllText(filePath);
            ArbParseResult parseResult = parser.ParseContent(content);
            if (parseResult.Document == null) {
                continue;
            }

            string locale = !string.IsNullOrWhiteSpace(parseResult.Document.Locale)
                ? parseResult.Document.Locale
                : StringHelper.FirstNonEmpty(
                    StringHelper.InferLangCodeFromFilename(Path.GetFileNameWithoutExtension(filePath)),
                    Path.GetFileNameWithoutExtension(filePath))
                  ?? Path.GetFileNameWithoutExtension(filePath);

            locale = NormalizeLocale(locale);
            parseResult.Document.Locale = locale;

            result.Add(new ArbLocaleDocument(locale, filePath, parseResult.Document));
        }

        return result;
    }

    private static string GuessMapping(string header, string? defaultLocale, IReadOnlyList<string> existingLocales) {
        string trimmed = header.Trim();
        if (trimmed.Length == 0) {
            return string.Empty;
        }

        if (IsKeyHeader(trimmed)) {
            return KEY_MAPPING;
        }

        if (IsIgnoredHeader(trimmed)) {
            return string.Empty;
        }

        if (IsDefaultCultureHeader(trimmed)) {
            return defaultLocale is { Length: > 0 }
                ? NormalizeLocale(defaultLocale)
                : string.Empty;
        }

        string? locale = GuessLocaleName(trimmed, existingLocales);
        return locale ?? string.Empty;
    }

    private static string? GuessLocaleName(string header, IReadOnlyList<string> existingLocales) {
        string normalizedHeader = NormalizeLocale(header);
        if (string.IsNullOrWhiteSpace(normalizedHeader)) {
            return null;
        }

        string? existingMatch = existingLocales.FirstOrDefault(locale =>
            string.Equals(NormalizeLocale(locale), normalizedHeader, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(existingMatch)) {
            return NormalizeLocale(existingMatch);
        }

        return IsLocaleLike(normalizedHeader)
            ? normalizedHeader
            : null;
    }

    private static bool IsKeyHeader(string header) {
        return string.Equals(header, "key", StringComparison.OrdinalIgnoreCase)
               || string.Equals(header, "name", StringComparison.OrdinalIgnoreCase)
               || string.Equals(header, "resource key", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsIgnoredHeader(string header) {
        return string.Equals(header, "path", StringComparison.OrdinalIgnoreCase)
               || string.Equals(header, "file", StringComparison.OrdinalIgnoreCase)
               || string.Equals(header, "folder", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDefaultCultureHeader(string header) {
        return string.Equals(header, "default culture", StringComparison.OrdinalIgnoreCase)
               || string.Equals(header, "defaultculture", StringComparison.OrdinalIgnoreCase)
               || string.Equals(header, "default locale", StringComparison.OrdinalIgnoreCase)
               || string.Equals(header, "defaultlocale", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLocaleLike(string value) {
        int underscoreCount = value.Count(c => c == '_');
        if (underscoreCount > 1) {
            return false;
        }

        string[] parts = value.Split(['_'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts.Length > 2) {
            return false;
        }

        return parts.All(part => part.Length is >= 2 and <= 8 && part.All(char.IsLetterOrDigit));
    }

    private static string NormalizeLocale(string locale) {
        return StringHelper.NormalizeLocale(locale).Trim();
    }

    private static bool IsLocaleMapping(string mapping) {
        return !string.IsNullOrWhiteSpace(mapping)
               && !string.Equals(mapping, KEY_MAPPING, StringComparison.OrdinalIgnoreCase);
    }

    private static List<List<string>> ParseCsv(string csvContent) {
        List<List<string>> rows = [];
        List<string> currentRow = [];
        StringBuilder currentCell = new();
        bool inQuotes = false;

        string content = csvContent.TrimStart('\uFEFF');
        for (int index = 0; index < content.Length; index++) {
            char current = content[index];

            if (inQuotes) {
                if (current == '"') {
                    if (index + 1 < content.Length && content[index + 1] == '"') {
                        currentCell.Append('"');
                        index++;
                    }
                    else {
                        inQuotes = false;
                    }
                }
                else {
                    currentCell.Append(current);
                }

                continue;
            }

            switch (current) {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    currentRow.Add(currentCell.ToString());
                    currentCell.Clear();
                    break;
                case '\r':
                    currentRow.Add(currentCell.ToString());
                    currentCell.Clear();
                    rows.Add(currentRow);
                    currentRow = [];
                    if (index + 1 < content.Length && content[index + 1] == '\n') {
                        index++;
                    }
                    break;
                case '\n':
                    currentRow.Add(currentCell.ToString());
                    currentCell.Clear();
                    rows.Add(currentRow);
                    currentRow = [];
                    break;
                default:
                    currentCell.Append(current);
                    break;
            }
        }

        if (inQuotes) {
            throw new InvalidOperationException("CSV contains an unterminated quoted field.");
        }

        currentRow.Add(currentCell.ToString());
        if (currentRow.Count > 1 || currentRow[0].Length > 0 || rows.Count == 0) {
            rows.Add(currentRow);
        }

        return rows;
    }

    private static void NormalizeRowWidths(List<List<string>> rows) {
        int width = rows.Max(row => row.Count);
        foreach (List<string> row in rows) {
            while (row.Count < width) {
                row.Add(string.Empty);
            }
        }
    }

    private static void WriteCsvRow(StringBuilder builder, IReadOnlyList<string> cells) {
        for (int index = 0; index < cells.Count; index++) {
            if (index > 0) {
                builder.Append(',');
            }

            builder.Append(EscapeCsvCell(cells[index]));
        }

        builder.AppendLine();
    }

    private static string EscapeCsvCell(string value) {
        if (value.IndexOfAny([',', '"', '\r', '\n']) < 0) {
            return value;
        }

        return '"' + value.Replace("\"", "\"\"") + '"';
    }

    private sealed class ArbLocaleDocument {
        public ArbLocaleDocument(string locale, string filePath, ArbDocument document) {
            Locale = locale;
            FilePath = filePath;
            Document = document;
        }

        public string Locale { get; }
        public string FilePath { get; }
        public ArbDocument Document { get; }
    }

    private sealed class CsvImportPayload(IReadOnlyList<string> locales) {
        public IReadOnlyList<string> Locales { get; } = locales;
        public List<string> OrderedKeys { get; } = [];
        public Dictionary<string, Dictionary<string, string>> ValuesByKey { get; } = new(StringComparer.Ordinal);
    }
}