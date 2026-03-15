using System.Xml.Linq;
using Arb.NET.Models;

namespace Arb.NET;

public static class ArbProjectGenerator {
    public sealed class Result {
        public List<string> WrittenFiles { get; } = [];
        public List<string> Errors { get; } = [];
        public bool HasErrors => Errors.Count > 0;
    }

    /// <summary>
    /// Generates .g.cs files for the project that owns <paramref name="projectDir"/>.
    /// If no <c>l10n.yaml</c> exists in <paramref name="projectDir"/>, parent directories
    /// are searched until one is found.
    /// </summary>
    public static Result Generate(string projectDir, IReadOnlyList<EnumLocalizationInfo>? enumLocalizations = null) {
        Result result = new();

        string? localizationFile = FindLocalizationYaml(projectDir);
        if (localizationFile == null) {
            result.Errors.Add($"No {Constants.LOCALIZATION_FILE} found in '{projectDir}' or any parent directory.");
            return result;
        }

        string configDir = Path.GetDirectoryName(localizationFile)!;
        string yaml = File.ReadAllText(localizationFile);
        L10nConfig config = L10nConfig.Parse(yaml);

        string arbDirAbsolute = Path.GetFullPath(Path.Combine(configDir, config.ArbDir));
        if (!Directory.Exists(arbDirAbsolute)) {
            result.Errors.Add($"arb-dir '{arbDirAbsolute}' does not exist.");
            return result;
        }

        string[] arbFiles = Directory.GetFiles(arbDirAbsolute, Constants.ANY_ARB);
        if (arbFiles.Length == 0) {
            result.Errors.Add($"No {Constants.ARB_FILE_EXT} files found in '{arbDirAbsolute}'.");
            return result;
        }

        string outputDir = Path.Combine(configDir, config.ArbDir);
        ProjectNamespaceInfo namespaceInfo = ResolveProjectNamespaceInfo(configDir);
        string namespaceName = ArbGeneration.ResolveNamespaceName(
            config,
            namespaceInfo.RootNamespace,
            namespaceInfo.AssemblyName,
            namespaceInfo.ProjectName);
        string? baseClass = string.IsNullOrWhiteSpace(config.OutputClass) ? null : config.OutputClass;

        string? defaultLangCode = ResolveDefaultLangCode(config, arbDirAbsolute);

        ArbParser parser = new();
        List<ArbGeneration.Input> generationInputs = [];

        foreach (string filePath in arbFiles.OrderBy(f => f, StringComparer.OrdinalIgnoreCase)) {
            string content = File.ReadAllText(filePath);
            ArbParseResult parseResult = parser.ParseContent(content);

            if (!parseResult.ValidationResults.IsValid) {
                foreach (ArbValidationError err in parseResult.ValidationResults.Errors) {
                    result.Errors.Add($"{filePath}: [{err.Keyword}] {err.Message}");
                }

                continue;
            }

            generationInputs.Add(new ArbGeneration.Input(
                                     Path.GetFileNameWithoutExtension(filePath),
                                     parseResult.Document!));
        }

        foreach (ArbGeneration.Output output in ArbGeneration.GenerateFiles(
                     generationInputs,
                     namespaceName,
                     baseClass,
                     defaultLangCode,
                     enumLocalizations)) {
            WriteFile(Path.Combine(outputDir, output.FileName), output.Content, result);
        }

        return result;
    }

    private static void WriteFile(string path, string content, Result result) {
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content, System.Text.Encoding.UTF8);
            result.WrittenFiles.Add(path);
        }
        catch (Exception ex) {
            result.Errors.Add($"Failed to write '{path}': {ex.Message}");
        }
    }

    private static ProjectNamespaceInfo ResolveProjectNamespaceInfo(string projectDir) {
        ProjectNamespaceInfo info = new() {
            ProjectName = new DirectoryInfo(projectDir).Name
        };

        try {
            string? projectFile = Directory.EnumerateFiles(projectDir, "*.csproj", SearchOption.TopDirectoryOnly)
                .OrderBy(path => GetProjectFilePriority(projectDir, path))
                .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (projectFile == null) {
                return info;
            }

            info.ProjectName = Path.GetFileNameWithoutExtension(projectFile);

            XDocument projectXml = XDocument.Load(projectFile);

            info.RootNamespace = projectXml.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "RootNamespace")?
                .Value;

            info.AssemblyName = projectXml.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "AssemblyName")?
                .Value;
        }
        catch {
            // Best-effort; callers have project-name fallback.
        }

        return info;
    }

    private static int GetProjectFilePriority(string projectDir, string projectFilePath) {
        string directoryName = new DirectoryInfo(projectDir).Name;
        string projectName = Path.GetFileNameWithoutExtension(projectFilePath);
        return string.Equals(directoryName, projectName, StringComparison.OrdinalIgnoreCase) ? 0 : 1;
    }

    private sealed class ProjectNamespaceInfo {
        public string? RootNamespace { get; set; }
        public string? AssemblyName { get; set; }
        public string? ProjectName { get; set; }
    }

    private static string? ResolveDefaultLangCode(L10nConfig config, string arbDirAbsolute) {
        if (string.IsNullOrWhiteSpace(config.TemplateArbFile)) {
            return null;
        }

        string templatePath = Path.Combine(arbDirAbsolute, config.TemplateArbFile);
        if (!File.Exists(templatePath)) {
            return null;
        }

        ArbParseResult templateParse = new ArbParser().ParseContent(File.ReadAllText(templatePath));
        if (templateParse.Document?.Locale != null) {
            return templateParse.Document.Locale;
        }

        return StringHelper.InferLangCodeFromFilename(Path.GetFileNameWithoutExtension(templatePath));
    }

    private static string? FindLocalizationYaml(string startDir) {
        string dir = startDir;
        while (true) {
            string candidate = Path.Combine(dir, Constants.LOCALIZATION_FILE);
            if (File.Exists(candidate)) return candidate;

            string? parent = Path.GetDirectoryName(dir);
            if (parent == null || parent == dir) return null;
            dir = parent;
        }
    }
}