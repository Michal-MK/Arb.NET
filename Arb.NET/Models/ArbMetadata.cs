namespace Arb.NET;

/// <summary>
/// Metadata for an .arb entry
/// </summary>
public record ArbMetadata {
    public string? Description { get; set; }
    public Dictionary<string, ArbPlaceholder>? Placeholders { get; set; }
}