namespace Arb.NET.Attributes;

/// <summary>
/// Instructs the Arb.NET source generator to prepare ARB localization entries for
/// all members of the decorated enum and generate a <c>Localize(EnumType)</c> overload
/// on the dispatcher class.
/// <para>
/// The optional <paramref name="description"/> is written into the <c>@metadata</c> block
/// of the <b>primary/default locale</b> ARB file only!
/// </para>
/// <para>
/// Generated ARB key format: <c>&lt;enumTypeName&gt;&lt;MemberName&gt;</c> in camelCase,
/// e.g. <c>MyStatus.Active</c> → <c>myStatusActive</c>.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Enum)]
public class ArbLocalizeAttribute(string? description = null) : Attribute {
    /// <summary>
    /// Optional translator note written into the <c>@metadata</c> description field
    /// of the primary locale ARB file for each generated entry.
    /// </summary>
    public string? Description { get; } = description;
}