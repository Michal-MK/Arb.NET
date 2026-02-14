namespace Arb.NET;

/// <summary>
/// Represents a parsed .arb document
/// </summary>
public record ArbDocument {
    public string Locale { get; set; } = string.Empty;
    public Dictionary<string, ArbEntry> Entries { get; set; } = new();
}