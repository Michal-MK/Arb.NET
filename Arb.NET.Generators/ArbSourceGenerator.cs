using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Arb.NET.Generators;

/// <summary>
/// Roslyn incremental source generator that turns .arb files (listed as AdditionalFiles
/// via &lt;ArbSource&gt; items) into strongly-typed C# localization classes.
///
/// When <c>ArbUseContextForSubclasses</c> is <c>false</c> and a base class name is
/// configured, a dispatcher class (e.g. <c>AppLocale</c>) is also generated.  The
/// dispatcher is a non-static class that accepts a <see cref="System.Globalization.CultureInfo"/>
/// in its constructor and routes every call to the correct locale-specific static class.
///
/// Consumer setup in their .csproj (all optional — driven by build props):
/// <code>
///   &lt;PropertyGroup&gt;
///     &lt;ArbDefaultClass&gt;AppLocale&lt;/ArbDefaultClass&gt;
///     &lt;ArbDefaultNamespace&gt;MyApp.Localizations&lt;/ArbDefaultNamespace&gt;
///     &lt;ArbDefaultLangCode&gt;en&lt;/ArbDefaultLangCode&gt;
///     &lt;ArbUseContextForSubclasses&gt;true&lt;/ArbUseContextForSubclasses&gt;
///   &lt;/PropertyGroup&gt;
///   &lt;ItemGroup&gt;
///     &lt;ArbSource Include="arbs\app_en.arb" /&gt;
///     &lt;ArbSource Include="arbs\Czech.arb" LangCode="cs" /&gt;
///   &lt;/ItemGroup&gt;
/// </code>
/// </summary>
[Generator]
public sealed class ArbSourceGenerator : IIncrementalGenerator {
    private static readonly DiagnosticDescriptor ParseError = new(
        id: "ARB001",
        title: "ARB parse error",
        messageFormat: "Failed to parse '{0}': {1}",
        category: "ArbGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context) {
        var arbFiles = context.AdditionalTextsProvider
            .Where(file => file.Path.EndsWith(".arb", StringComparison.OrdinalIgnoreCase));

        var combined = arbFiles.Combine(context.AnalyzerConfigOptionsProvider);

        var allFiles = combined.Collect();

        context.RegisterSourceOutput(allFiles, (ctx, pairs) => {
            // ── Per-file pass: parse + emit locale-specific classes ──
            // We also accumulate enough information to later emit the dispatcher.

            // Key: baseClassName
            Dictionary<string, List<(ArbDocument Document, string ClassName, string NamespaceName, string DefaultLangCode, bool UseContext)>> groups = new();

            foreach ((AdditionalText file, AnalyzerConfigOptionsProvider optionsProvider) in pairs) {
                string? content = file.GetText(ctx.CancellationToken)?.ToString();
                if (string.IsNullOrWhiteSpace(content))
                    continue;

                ArbParseResult result = new ArbParser().ParseContent(content!);

                if (!result.ValidationResults.IsValid) {
                    foreach (var error in result.ValidationResults.Errors)
                        ctx.ReportDiagnostic(Diagnostic.Create(ParseError, Location.None, file.Path, error.Keyword, error.Message, error.InstanceLocation, error.SchemaPath));
                    continue;
                }

                ArbDocument document = result.Document!;

                string? fileNameWithoutExt = Path.GetFileNameWithoutExtension(file.Path);
                AnalyzerConfigOptions fileOptions = optionsProvider.GetOptions(file);
                AnalyzerConfigOptions globalOptions = optionsProvider.GlobalOptions;

                // ── Resolve namespace ──
                string namespaceName = FirstNonEmpty(
                    GetFileMeta(fileOptions, "Namespace"),
                    GetFileMeta(fileOptions, "DefaultNamespace"),
                    GetGlobalProp(globalOptions, "ArbDefaultNamespace"),
                    GetGlobalProp(globalOptions, "RootNamespace"),
                    "Arb.Generated")!;

                // ── Resolve language code ──
                string? langCode = FirstNonEmpty(
                    GetFileMeta(fileOptions, "LangCode"),
                    InferLangCodeFromFilename(fileNameWithoutExt),
                    document.Locale,
                    GetGlobalProp(globalOptions, "ArbDefaultLangCode"));

                if (!string.IsNullOrWhiteSpace(langCode))
                    document.Locale = langCode!;

                // ── Resolve class name ──
                string? explicitClassName = GetFileMeta(fileOptions, "ClassName");

                string className;
                string? baseClass = null;

                if (!string.IsNullOrWhiteSpace(explicitClassName)) {
                    string localeSuffix = StringHelper.NormalizeLocale(document.Locale);
                    className = string.IsNullOrWhiteSpace(localeSuffix)
                        ? explicitClassName!
                        : explicitClassName + "_" + localeSuffix;
                }
                else {
                    baseClass = FirstNonEmpty(
                        GetFileMeta(fileOptions, "DefaultClass"),
                        GetGlobalProp(globalOptions, "ArbDefaultClass"));

                    if (!string.IsNullOrWhiteSpace(baseClass)) {
                        bool useContext = StringHelper.IsTrue(
                            FirstNonEmpty(
                                GetFileMeta(fileOptions, "UseContextForSubclasses"),
                                GetGlobalProp(globalOptions, "ArbUseContextForSubclasses")
                            )
                        );

                        string? defaultLangCode = FirstNonEmpty(
                            GetFileMeta(fileOptions, "DefaultLangCode"),
                            GetGlobalProp(globalOptions, "ArbDefaultLangCode")
                        );

                        string localeSuffix = StringHelper.NormalizeLocale(document.Locale);

                        bool isDefaultLocale = useContext
                                               && !string.IsNullOrWhiteSpace(defaultLangCode)
                                               && StringHelper.NormalizeLocale(defaultLangCode) == localeSuffix;

                        className = isDefaultLocale || string.IsNullOrWhiteSpace(localeSuffix)
                            ? baseClass!
                            : baseClass + "_" + localeSuffix;
                    }
                    else {
                        className = string.Concat(Array.ConvertAll(fileNameWithoutExt.Split('_'), StringHelper.ToPascalCase))
                                    + "Localizations";
                    }
                }

                // Emit the locale-specific class
                string source = new ArbCodeGenerator().GenerateClass(document, className, namespaceName);
                ctx.AddSource($"{fileNameWithoutExt}.g.cs", source);

                // Accumulate for dispatcher generation
                if (!string.IsNullOrWhiteSpace(baseClass)) {
                    bool useContextForGroup = StringHelper.IsTrue(
                        FirstNonEmpty(
                            GetFileMeta(fileOptions, "UseContextForSubclasses"),
                            GetGlobalProp(globalOptions, "ArbUseContextForSubclasses")
                        )
                    );

                    string defaultLangCodeForGroup = FirstNonEmpty(
                        GetFileMeta(fileOptions, "DefaultLangCode"),
                        GetGlobalProp(globalOptions, "ArbDefaultLangCode")) ?? string.Empty;

                    if (!groups.TryGetValue(baseClass!, out List<(ArbDocument Document, string ClassName, string NamespaceName, string DefaultLangCode, bool UseContext)>? list)) {
                        list = [];
                        groups[baseClass!] = list;
                    }
                    list.Add((document, className, namespaceName, defaultLangCodeForGroup, useContextForGroup));
                }
            }

            // ── Dispatcher pass ──
            foreach (var groupPair in groups) {
                string? baseClassName = groupPair.Key;
                List<(ArbDocument Document, string ClassName, string NamespaceName, string DefaultLangCode, bool UseContext)>? entries = groupPair.Value;

                // Only emit dispatcher when ArbUseContextForSubclasses is false
                // (if any entry in the group has it true, skip — mixed config is unsupported)
                if (entries.Any(e => e.UseContext))
                    continue;

                string? defaultLangCode = entries.FirstOrDefault(e => !string.IsNullOrEmpty(e.DefaultLangCode)).DefaultLangCode;
                string? namespaceName = entries[0].NamespaceName;

                // Find the default-locale document (authoritative for entry signatures)
                ArbDocument? defaultDocument = null;
                if (!string.IsNullOrWhiteSpace(defaultLangCode)) {
                    string normalizedDefault = StringHelper.NormalizeLocale(defaultLangCode);
                    defaultDocument = entries
                        .FirstOrDefault(e => StringHelper.NormalizeLocale(e.Document.Locale) == normalizedDefault)
                        .Document;
                }
                defaultDocument ??= entries[0].Document;

                List<(ArbDocument Document, string ClassName)> locales = entries
                    .Select(e => (e.Document, e.ClassName))
                    .ToList();

                string dispatcher = new ArbCodeGenerator().GenerateDispatcherClass(
                    locales,
                    defaultDocument,
                    baseClassName,
                    namespaceName
                );

                ctx.AddSource($"{baseClassName}Dispatcher.g.cs", dispatcher);
            }
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────────

    private static string? GetFileMeta(AnalyzerConfigOptions options, string key) {
        options.TryGetValue($"build_metadata.AdditionalFiles.{key}", out string? value);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? GetGlobalProp(AnalyzerConfigOptions options, string key) {
        options.TryGetValue($"build_property.{key}", out string? value);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? FirstNonEmpty(params string?[] values) {
        return values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
    }

    private static string? InferLangCodeFromFilename(string fileNameWithoutExt) {
        string[] segments = fileNameWithoutExt.Split('_');
        return segments.Length <= 1
            ? null
            : string.Join("_", segments, 1, segments.Length - 1);
    }
}