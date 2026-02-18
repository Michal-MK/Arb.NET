namespace Arb.NET.Tool.Migration;

/// <summary>
/// Represents a group of related .resx files sharing the same base name
/// (e.g. Strings.resx, Strings.fr.resx, Strings.de.resx).
/// </summary>
internal sealed class ResxGroup
{
    public ResxGroup(string directory, string baseStem)
    {
        Directory = directory;
        BaseStem = baseStem;
    }

    public string Directory { get; }
    public string BaseStem { get; }
    public string BaseName => Path.Combine(Directory, BaseStem + ".resx");

    /// <summary>The default (neutral culture) .resx file, if present.</summary>
    public string? DefaultFile { get; set; }

    /// <summary>Locale-specific .resx files keyed by locale code (e.g. "fr", "zh-CN").</summary>
    public Dictionary<string, string> LocaleFiles { get; } = new(StringComparer.OrdinalIgnoreCase);
}
