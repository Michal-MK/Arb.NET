namespace Arb.NET;

public record EnumLocalizationInfo {

    public string FullName { get; set; } = string.Empty;
    public string SimpleName { get; set; } = string.Empty;
    public IReadOnlyList<string> Members { get; set; } = [];

    /// <summary>Translator note from <c>[ArbLocalize("...")]</c></summary>
    public string? Description { get; set; }
}