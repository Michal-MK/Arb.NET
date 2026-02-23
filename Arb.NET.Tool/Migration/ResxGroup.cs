namespace Arb.NET.Tool.Migration;

internal sealed class ResxGroup(string directory, string baseStem) {

    public string Directory { get; } = directory;
    public string BaseStem { get; } = baseStem;
    public string BaseName => Path.Combine(Directory, BaseStem + ".resx");

    /// <summary>The default (neutral culture) .resx file, if present.</summary>
    public string? DefaultFile { get; set; }

    /// <summary>Locale-specific .resx files keyed by locale code (e.g. "fr", "zh-CN").</summary>
    public Dictionary<string, string> LocaleFiles { get; } = new(StringComparer.OrdinalIgnoreCase);
}