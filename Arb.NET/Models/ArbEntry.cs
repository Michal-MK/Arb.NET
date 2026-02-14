namespace Arb.NET;

/// <summary>
/// Represents a single localization entry in an .arb file
/// </summary>
public record ArbEntry {
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public ArbMetadata? Metadata { get; set; }
}