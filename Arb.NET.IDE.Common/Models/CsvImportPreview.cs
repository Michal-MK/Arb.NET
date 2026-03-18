namespace Arb.NET.IDE.Common.Models;

public sealed class CsvImportPreview {
    public List<string> Headers { get; } = [];
    public List<CsvImportPreviewRow> Rows { get; } = [];
    public List<string> SuggestedMappings { get; } = [];
    public List<string> AvailableLocaleMappings { get; } = [];
    public string? DefaultLocale { get; set; }
}

public sealed class CsvImportPreviewRow {
    public List<string> Cells { get; } = [];
}