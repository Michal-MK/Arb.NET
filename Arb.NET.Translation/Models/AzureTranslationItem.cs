namespace Arb.NET.Translation.Models;

public class AzureTranslationItem {
    public string Key { get; set; } = string.Empty;
    public string SourceText { get; set; } = string.Empty;
    public string? Description { get; set; }
}