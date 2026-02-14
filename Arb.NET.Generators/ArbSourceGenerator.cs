using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Arb.NET.Generators;

/// <summary>
/// Roslyn incremental source generator that turns .arb files (listed as AdditionalFiles
/// via &lt;ArbSource&gt; items) into strongly-typed C# localization classes.
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
        // Filter AdditionalFiles to only .arb files
        var arbFiles = context.AdditionalTextsProvider
            .Where(file => file.Path.EndsWith(".arb", StringComparison.OrdinalIgnoreCase));

        // Combine each arb file with the global analyzer config options
        var combined = arbFiles.Combine(context.AnalyzerConfigOptionsProvider);

        context.RegisterSourceOutput(combined, static (ctx, pair) => {
            var (file, optionsProvider) = pair;

            var content = file.GetText(ctx.CancellationToken)?.ToString();
            if (string.IsNullOrWhiteSpace(content))
                return;

            ArbParseResult result = new ArbParser().ParseContent(content!);

            if (!result.ValidationResults.IsValid) {
                foreach (var error in result.ValidationResults.Errors)
                    ctx.ReportDiagnostic(Diagnostic.Create(ParseError, Location.None, file.Path, error.Keyword, error.Message, error.InstanceLocation, error.SchemaPath));
                return;
            }
            
            var document = result.Document!;

            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(file.Path);
            var fileOptions = optionsProvider.GetOptions(file);
            var globalOptions = optionsProvider.GlobalOptions;

            // ── Namespace ─────────────────────────────────────────────────────────────
            // Priority: per-file Namespace > per-file DefaultNamespace > build_property.ArbDefaultNamespace > RootNamespace > fallback
            var namespaceName = FirstNonEmpty(
                GetFileMeta(fileOptions, "Namespace"),
                GetFileMeta(fileOptions, "DefaultNamespace"),
                GetGlobalProp(globalOptions, "ArbDefaultNamespace"),
                GetGlobalProp(globalOptions, "RootNamespace"),
                "Arb.Generated");

            // ── Lang code ─────────────────────────────────────────────────────────────
            // Priority: per-file LangCode > inferred from filename > @@locale in file > ArbDefaultLangCode
            // Note: DefaultLangCode is the *project* default used for class-naming decisions only —
            // it must NOT override the locale inferred from the filename.
            var langCode = FirstNonEmpty(
                GetFileMeta(fileOptions, "LangCode"),
                InferLangCodeFromFilename(fileNameWithoutExt),
                document.Locale,
                GetGlobalProp(globalOptions, "ArbDefaultLangCode"));

            if (!string.IsNullOrWhiteSpace(langCode))
                document.Locale = langCode!;

            // ── Class name ────────────────────────────────────────────────────────────
            // Priority: per-file ClassName (legacy explicit override) > DefaultClass logic > auto-derive from filename

            var explicitClassName = GetFileMeta(fileOptions, "ClassName");

            string className;
            if (!string.IsNullOrWhiteSpace(explicitClassName)) {
                // Legacy: caller gave a full explicit class name; append locale suffix to disambiguate.
                var localeSuffix = NormalizeLocale(document.Locale);
                className = string.IsNullOrWhiteSpace(localeSuffix)
                    ? explicitClassName!
                    : explicitClassName + "_" + localeSuffix;
            }
            else {
                // Resolve base class name: per-file DefaultClass > build_property.ArbDefaultClass
                var baseClass = FirstNonEmpty(
                    GetFileMeta(fileOptions, "DefaultClass"),
                    GetGlobalProp(globalOptions, "ArbDefaultClass"));

                if (!string.IsNullOrWhiteSpace(baseClass)) {
                    // ArbUseContextForSubclasses: when true, the file whose locale matches the
                    // project default lang code gets the plain base class name; all others get
                    // a locale suffix.  When false (or unset), all files always get a suffix.
                    var useContext = IsTrue(FirstNonEmpty(
                                                GetFileMeta(fileOptions, "UseContextForSubclasses"),
                                                GetGlobalProp(globalOptions, "ArbUseContextForSubclasses")));

                    var defaultLangCode = FirstNonEmpty(
                        GetFileMeta(fileOptions, "DefaultLangCode"),
                        GetGlobalProp(globalOptions, "ArbDefaultLangCode"));

                    var localeSuffix = NormalizeLocale(document.Locale);

                    bool isDefaultLocale = useContext
                                           && !string.IsNullOrWhiteSpace(defaultLangCode)
                                           && NormalizeLocale(defaultLangCode) == localeSuffix;

                    className = isDefaultLocale || string.IsNullOrWhiteSpace(localeSuffix)
                        ? baseClass!
                        : baseClass + "_" + localeSuffix;
                }
                else {
                    // No base class configured — derive from all filename segments.
                    className = string.Concat(System.Array.ConvertAll(fileNameWithoutExt.Split('_'), ToPascalCase))
                                + "Localizations";
                }
            }

            var source = new ArbCodeGenerator().GenerateClass(document, className, namespaceName!);
            ctx.AddSource($"{fileNameWithoutExt}_localizations.g.cs", source);
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────────

    private static string? GetFileMeta(AnalyzerConfigOptions options, string key) {
        options.TryGetValue($"build_metadata.AdditionalFiles.{key}", out var value);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? GetGlobalProp(AnalyzerConfigOptions options, string key) {
        options.TryGetValue($"build_property.{key}", out var value);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? FirstNonEmpty(params string?[] values) {
        foreach (var v in values)
            if (!string.IsNullOrWhiteSpace(v))
                return v;
        return null;
    }

    /// <summary>
    /// Infers a locale string from the filename segments after the first underscore.
    /// "app_en" → "en", "app_en_US" → "en_US", "Czech" → null (no underscore).
    /// </summary>
    private static string? InferLangCodeFromFilename(string fileNameWithoutExt) {
        var segments = fileNameWithoutExt.Split('_');
        if (segments.Length <= 1) return null;
        return string.Join("_", segments, 1, segments.Length - 1);
    }

    /// <summary>Normalises a locale string for use as a class name suffix.</summary>
    private static string NormalizeLocale(string? locale) {
        if (string.IsNullOrWhiteSpace(locale)) return string.Empty;
        return locale!.Replace("-", "_").Replace(" ", string.Empty);
    }

    private static bool IsTrue(string? value) =>
        string.Equals(value, "true", System.StringComparison.OrdinalIgnoreCase);

    private static string ToPascalCase(string value) {
        if (string.IsNullOrEmpty(value)) return value;
        return char.ToUpperInvariant(value[0]) + value.Substring(1);
    }
}