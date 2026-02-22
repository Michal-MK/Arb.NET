namespace Arb.NET.Models;

// ReSharper disable once InconsistentNaming
public class L10nConfig {
    
    public const string DEFAULT_ARB_DIR = "arbs";

    /// <summary>Relative to the project root.</summary>
    public string ArbDir { get; set; } = DEFAULT_ARB_DIR;

    public string TemplateArbFile { get; set; } = string.Empty;

    /// <summary>Base name for generated locale classes and the dispatcher (e.g. "AppLocale").</summary>
    public string? OutputClass { get; set; }

    /// <summary>Namespace for all generated code. Falls back to the project's RootNamespace.</summary>
    public string? OutputNamespace { get; set; }

    public static L10nConfig Parse(string yaml) {
        Dictionary<string, string> dict = new(StringComparer.OrdinalIgnoreCase);

        foreach (string raw in yaml.Split('\n')) {
            string line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;

            int colon = line.IndexOf(':');
            if (colon <= 0) continue;

            string key = line.Substring(0, colon).Trim();
            string val = line.Substring(colon + 1).Trim().Trim('"', '\'');

            if (val.Length > 0) {
                dict[key] = val;
            }
        }

        dict.TryGetValue("arb-dir", out string? arbDir);
        dict.TryGetValue("template-arb-file", out string? templateArbFile);
        dict.TryGetValue("output-class", out string? outputClass);
        dict.TryGetValue("output-namespace", out string? outputNamespace);

        return new L10nConfig {
            ArbDir = arbDir ?? DEFAULT_ARB_DIR,
            TemplateArbFile = templateArbFile ?? string.Empty,
            OutputClass = string.IsNullOrWhiteSpace(outputClass) ? null : outputClass,
            OutputNamespace = string.IsNullOrWhiteSpace(outputNamespace) ? null : outputNamespace
        };
    }
}