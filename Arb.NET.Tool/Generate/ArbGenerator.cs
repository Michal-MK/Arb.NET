using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arb.NET;
using Arb.NET.Models;

namespace Arb.NET.Tool.Generate;

public static class ArbGenerator {

    public sealed class Result {
        public List<string> WrittenFiles { get; } = [];
        public List<string> Errors { get; } = [];
        public bool HasErrors => Errors.Count > 0;
    }

    /// <summary>
    /// Generates .g.cs files for all ARB projects found under <paramref name="projectDir"/>.
    /// Walks up from <paramref name="projectDir"/> to find an l10n.yaml if none exists there.
    /// </summary>
    public static Result Generate(string projectDir) {
        Result result = new();

        string? l10nPath = FindL10nYaml(projectDir);
        if (l10nPath == null) {
            result.Errors.Add($"No l10n.yaml found in '{projectDir}' or any parent directory.");
            return result;
        }

        string configDir = Path.GetDirectoryName(l10nPath)!;
        string yaml = File.ReadAllText(l10nPath);
        L10nConfig config = L10nConfig.Parse(yaml);

        string arbDirAbsolute = Path.GetFullPath(Path.Combine(configDir, config.ArbDir));
        if (!Directory.Exists(arbDirAbsolute)) {
            result.Errors.Add($"arb-dir '{arbDirAbsolute}' does not exist.");
            return result;
        }

        string[] arbFiles = Directory.GetFiles(arbDirAbsolute, "*.arb");
        if (arbFiles.Length == 0) {
            result.Errors.Add($"No .arb files found in '{arbDirAbsolute}'.");
            return result;
        }

        string outputDir = Path.Combine(configDir, config.ArbDir);

        string namespaceName = FirstNonEmpty(
            config.OutputNamespace,
            "Arb.Generated")!;

        string? baseClass = string.IsNullOrWhiteSpace(config.OutputClass) ? null : config.OutputClass;

        // Determine default lang code from the template file.
        string? defaultLangCode = null;
        if (!string.IsNullOrWhiteSpace(config.TemplateArbFile)) {
            string templatePath = Path.Combine(arbDirAbsolute, config.TemplateArbFile);
            if (File.Exists(templatePath)) {
                ArbParseResult templateParse = new ArbParser().Parse(templatePath);
                if (templateParse.Document?.Locale != null) {
                    defaultLangCode = templateParse.Document.Locale;
                }
                defaultLangCode ??= InferLangCodeFromFilename(Path.GetFileNameWithoutExtension(templatePath));
            }
        }

        ArbParser parser = new();
        ArbCodeGenerator generator = new();

        List<(ArbDocument Document, string ClassName)> localeList = [];

        foreach (string filePath in arbFiles.OrderBy(f => f)) {
            string content = File.ReadAllText(filePath);
            ArbParseResult parseResult = parser.ParseContent(content);

            if (!parseResult.ValidationResults.IsValid) {
                foreach (ArbValidationError err in parseResult.ValidationResults.Errors) {
                    result.Errors.Add($"{filePath}: [{err.Keyword}] {err.Message}");
                }
                continue;
            }

            ArbDocument document = parseResult.Document!;
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);

            string? langCode = FirstNonEmpty(
                InferLangCodeFromFilename(fileNameWithoutExt),
                document.Locale,
                defaultLangCode);

            if (!string.IsNullOrWhiteSpace(langCode)) {
                document.Locale = langCode!;
            }

            string className;
            if (!string.IsNullOrWhiteSpace(baseClass)) {
                string localeSuffix = StringHelper.NormalizeLocale(document.Locale);
                className = string.IsNullOrWhiteSpace(localeSuffix)
                    ? baseClass!
                    : baseClass + "_" + localeSuffix;
            }
            else {
                className = string.Concat(Array.ConvertAll(fileNameWithoutExt.Split('_'), StringHelper.ToPascalCase))
                            + "Localizations";
            }

            string source = generator.GenerateClass(document, className, namespaceName);
            string outPath = Path.Combine(outputDir, $"{fileNameWithoutExt}.g.cs");

            WriteFile(outPath, source, result);

            if (!string.IsNullOrWhiteSpace(baseClass)) {
                localeList.Add((document, className));
            }
        }

        // Emit dispatcher.
        if (baseClass != null && localeList.Count > 0) {
            ArbDocument? defaultDocument = null;
            if (!string.IsNullOrWhiteSpace(defaultLangCode)) {
                string normalizedDefault = StringHelper.NormalizeLocale(defaultLangCode);
                defaultDocument = localeList
                    .FirstOrDefault(l => StringHelper.NormalizeLocale(l.Document.Locale) == normalizedDefault)
                    .Document;
            }
            defaultDocument ??= localeList[0].Document;

            string dispatcher = generator.GenerateDispatcherClass(
                localeList,
                defaultDocument,
                baseClass,
                namespaceName);

            string dispatcherPath = Path.Combine(outputDir, $"{baseClass}Dispatcher.g.cs");
            WriteFile(dispatcherPath, dispatcher, result);
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

    private static string? FindL10nYaml(string startDir) {
        string dir = startDir;
        while (true) {
            string candidate = Path.Combine(dir, "l10n.yaml");
            if (File.Exists(candidate)) return candidate;

            string? parent = Path.GetDirectoryName(dir);
            if (parent == null || parent == dir) return null;
            dir = parent;
        }
    }

    private static string? InferLangCodeFromFilename(string fileNameWithoutExt) {
        string[] segments = fileNameWithoutExt.Split('_');
        return segments.Length <= 1
            ? null
            : string.Join("_", segments, 1, segments.Length - 1);
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
