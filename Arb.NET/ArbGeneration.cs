using Arb.NET.Models;

namespace Arb.NET;

public static class ArbGeneration {
    public sealed class Input(string fileNameWithoutExt, ArbDocument document) {
        public string FileNameWithoutExt { get; } = fileNameWithoutExt;
        public ArbDocument Document { get; } = document;
    }

    public sealed class Output(string fileName, string content) {
        public string FileName { get; } = fileName;
        public string Content { get; } = content;
    }

    public static string ResolveNamespaceName(
        L10nConfig config,
        string? rootNamespace,
        string? assemblyName,
        string? projectName) {
        return StringHelper.FirstNonEmpty(
            config.OutputNamespace,
            rootNamespace,
            assemblyName,
            projectName,
            "Arb.Generated")!;
    }

    public static IReadOnlyList<Output> GenerateFiles(
        IEnumerable<Input> inputs,
        string namespaceName,
        string? baseClass,
        string? defaultLangCode,
        IReadOnlyList<EnumLocalizationInfo>? enumLocalizations = null
    ) {
        ArbCodeGenerator generator = new();
        List<Output> outputs = [];
        List<(ArbDocument Document, string ClassName)> localeList = [];

        foreach (Input input in inputs.OrderBy(i => i.FileNameWithoutExt, StringComparer.OrdinalIgnoreCase)) {
            ArbDocument document = input.Document;

            string? langCode = StringHelper.FirstNonEmpty(
                StringHelper.InferLangCodeFromFilename(input.FileNameWithoutExt),
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
                className = string.Concat(Array.ConvertAll(input.FileNameWithoutExt.Split('_'), StringHelper.ToPascalCase))
                            + "Localizations";
            }

            outputs.Add(new Output(
                            $"{input.FileNameWithoutExt}.g.cs",
                            generator.GenerateClass(document, className, namespaceName)));

            if (!string.IsNullOrWhiteSpace(baseClass)) {
                localeList.Add((document, className));
            }
        }

        if (baseClass != null && localeList.Count > 0) {
            ArbDocument? defaultDocument = null;
            if (!string.IsNullOrWhiteSpace(defaultLangCode)) {
                string normalizedDefault = StringHelper.NormalizeLocale(defaultLangCode);
                defaultDocument = localeList
                    .FirstOrDefault(l => StringHelper.NormalizeLocale(l.Document.Locale) == normalizedDefault)
                    .Document;
            }

            defaultDocument ??= localeList[0].Document;

            outputs.Add(new Output(
                            $"{baseClass}Dispatcher.g.cs",
                            generator.GenerateDispatcherClass(
                                localeList,
                                defaultDocument,
                                baseClass,
                                namespaceName,
                                enumLocalizations)));
        }

        return outputs;
    }
}