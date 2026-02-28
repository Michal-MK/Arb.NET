namespace Arb.NET.IDE.Common.Models;

/// <summary>
/// Describes a single ARB key discovered from a project's generated dispatcher or template .arb file.
/// </summary>
public sealed class ArbKeyInfo(
    string key,
    bool isParametric,
    string? description,
    string? arbFilePath,
    string? rawKey,
    int lineNumber = -1,
    string? xmlDoc = null)
{
    /// <summary>PascalCase key name (as it appears in the generated dispatcher).</summary>
    public string Key { get; } = key;

    /// <summary>True when the key corresponds to a parameterised method rather than a plain property.</summary>
    public bool IsParametric { get; } = isParametric;

    /// <summary>Human-readable description from the @-metadata block in the .arb file, if any.</summary>
    public string? Description { get; } = description;

    /// <summary>Absolute path to the template .arb file, or null when unavailable.</summary>
    public string? ArbFilePath { get; } = arbFilePath;

    /// <summary>Original camelCase key as it appears in the .arb file.</summary>
    public string? RawKey { get; } = rawKey;

    /// <summary>0-based line index of the key in the .arb file, or -1 when unknown.</summary>
    public int LineNumber { get; } = lineNumber;

    /// <summary>Raw inner content of the &lt;summary&gt; XML doc tag from the generated dispatcher, or null when unavailable.</summary>
    public string? XmlDoc { get; } = xmlDoc;
}
