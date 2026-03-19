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

        // First pass: collect documents and class names
        List<(Input input, ArbDocument document, string className)> prepared = [];
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

            prepared.Add((input, document, className));
            if (!string.IsNullOrWhiteSpace(baseClass)) {
                localeList.Add((document, className));
            }
        }

        // Issue 2: Build cross-locale parameter map — for each key, find the most complete parameter list
        // Skip entries with mangled plurals — their extracted params are garbage
        Dictionary<string, List<ArbParameterDefinition>>? paramOverrides = null;
        if (localeList.Count > 1) {
            paramOverrides = new Dictionary<string, List<ArbParameterDefinition>>();
            foreach ((ArbDocument doc, string _) in localeList) {
                foreach (ArbEntry entry in doc.Entries.Values) {
                    if (entry.TryDetectMangledPlural(out _)) continue;
                    if (entry.IsParametric(out List<ArbParameterDefinition> defs)) {
                        if (!paramOverrides.TryGetValue(entry.Key, out List<ArbParameterDefinition>? existing) ||
                            defs.Count > existing.Count) {
                            paramOverrides[entry.Key] = defs;
                        }
                    }
                }
            }
        }

        // Second pass: generate code with cross-locale awareness
        foreach ((Input input, ArbDocument document, string className) in prepared) {
            outputs.Add(new Output(
                            $"{input.FileNameWithoutExt}.g.cs",
                            generator.GenerateClass(document, className, namespaceName, paramOverrides)));
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