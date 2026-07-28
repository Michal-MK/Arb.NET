namespace Arb.NET.Models;

public sealed class CsvImportApplyResult {
    public int ImportedKeyCount { get; set; }
    public int AffectedLocaleCount { get; set; }
    public int CreatedLocaleCount { get; set; }
}