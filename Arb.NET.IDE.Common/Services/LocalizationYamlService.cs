namespace Arb.NET.IDE.Common.Services;

public static class LocalizationYamlService {
    /// <summary>
    /// Reads the <c>arb-dir:</c> entry from <c>l10n.yaml</c> in <paramref name="projectDir"/>
    /// and returns the resolved absolute path to the ARB directory.
    /// Falls back to <paramref name="projectDir"/> if the file is missing or the entry is absent.
    /// </summary>
    public static string ResolveArbDirectory(string projectDir) {
        string localizationPath = Path.Combine(projectDir, "l10n.yaml");
        if (!File.Exists(localizationPath)) return projectDir;

        foreach (string line in File.ReadLines(localizationPath)) {
            string trimmed = line.Trim().TrimStart('\uFEFF');
            if (!trimmed.StartsWith("arb-dir:")) continue;

            string configured = trimmed.Substring("arb-dir:".Length).Trim().Trim('"', '\'');
            if (string.IsNullOrEmpty(configured)) return projectDir;

            string full = Path.GetFullPath(Path.Combine(projectDir, configured));
            return Directory.Exists(full) ? full : projectDir;
        }

        return projectDir;
    }
}