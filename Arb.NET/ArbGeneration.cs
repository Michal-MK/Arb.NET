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
        string defaultLocaleKey = StringHelper.NormalizeLocale(defaultLangCode);
        foreach (Input input in inputs.OrderBy(i => {
                     string locale = StringHelper.NormalizeLocale(
                         StringHelper.FirstNonEmpty(i.Document.Locale,
                             StringHelper.InferLangCodeFromFilename(i.FileNameWithoutExt)) ?? i.FileNameWithoutExt);
                     // Default locale first (0_), then variants of it second (1_), then the rest (2_).
                     if (locale == defaultLocaleKey) return "0_";
                     if (!string.IsNullOrEmpty(defaultLocaleKey) && locale.EndsWith("_" + defaultLocaleKey)) return "1_" + locale;
                     return "2_" + locale;
                 }, StringComparer.OrdinalIgnoreCase)) {
            ArbDocument document = input.Document;

            string? langCode = StringHelper.FirstNonEmpty(
                document.Locale,
                StringHelper.InferLangCodeFromFilename(input.FileNameWithoutExt),
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
            // Guard against duplicate locales (e.g. from duplicate AdditionalFiles entries)
            localeList = localeList
                .GroupBy(l => StringHelper.NormalizeLocale(l.Document.Locale), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
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