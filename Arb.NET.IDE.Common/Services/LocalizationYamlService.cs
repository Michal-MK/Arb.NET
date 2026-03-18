using Arb.NET.Models;

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

    public static string? FindProjectDirectory(string startDir) {
        string dir = startDir;
        while (true) {
            if (File.Exists(Path.Combine(dir, Constants.LOCALIZATION_FILE))) {
                return dir;
            }

            string? parent = Path.GetDirectoryName(dir);
            if (parent == null || parent == dir) {
                return null;
            }

            dir = parent;
        }
    }

    public static string? ResolveDefaultLocaleForArbDirectory(string arbDirectory) {
        string? projectDir = FindProjectDirectory(arbDirectory);
        return projectDir == null
            ? null
            : ResolveDefaultLocale(projectDir);
    }

    public static string? ResolveDefaultLocale(string projectDir) {
        string localizationPath = Path.Combine(projectDir, Constants.LOCALIZATION_FILE);
        if (!File.Exists(localizationPath)) {
            return null;
        }

        L10nConfig config = L10nConfig.Parse(File.ReadAllText(localizationPath));
        if (string.IsNullOrWhiteSpace(config.TemplateArbFile)) {
            return null;
        }

        string arbDirectory = ResolveArbDirectory(projectDir);
        string templatePath = Path.Combine(arbDirectory, config.TemplateArbFile);
        if (!File.Exists(templatePath)) {
            return null;
        }

        ArbParseResult parseResult = new ArbParser().ParseContent(File.ReadAllText(templatePath));
        return !string.IsNullOrWhiteSpace(parseResult.Document?.Locale)
            ? StringHelper.NormalizeLocale(parseResult.Document!.Locale)
            : StringHelper.NormalizeLocale(
                StringHelper.FirstNonEmpty(
                    StringHelper.InferLangCodeFromFilename(Path.GetFileNameWithoutExtension(templatePath)),
                    Path.GetFileNameWithoutExtension(templatePath)));
    }
}