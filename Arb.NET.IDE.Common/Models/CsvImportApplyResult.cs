namespace Arb.NET.IDE.Common.Models;

public sealed class CsvImportApplyResult {
    public int ImportedKeyCount { get; set; }
    public int AffectedLocaleCount { get; set; }
    public int CreatedLocaleCount { get; set; }
}