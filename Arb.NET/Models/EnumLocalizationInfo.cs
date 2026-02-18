namespace Arb.NET;

internal sealed class EnumLocalizationInfo(string fullName, string simpleName, IReadOnlyList<string> members, string? description = null) {

    public string FullName { get; } = fullName;
    public string SimpleName { get; } = simpleName;
    public IReadOnlyList<string> Members { get; } = members;

    /// <summary>Translator note from <c>[ArbLocalize("...")]</c></summary>
    public string? Description { get; } = description;
}