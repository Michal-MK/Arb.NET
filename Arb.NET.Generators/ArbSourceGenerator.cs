using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Arb.NET.Generators;

/// <summary>
/// Roslyn incremental source generator that turns .arb files (listed as AdditionalFiles
/// via &lt;ArbSource&gt; items) into strongly-typed C# localization classes.
///
/// When <c>ArbDefaultClass</c> is set (e.g. <c>AppLocale</c>), locale-specific static
/// classes are generated with that name as a prefix (e.g. <c>AppLocale_en</c>,
/// <c>AppLocale_cs</c>), and a dispatcher class using the bare base name (<c>AppLocale</c>)
/// is also generated.  The dispatcher accepts a <see cref="System.Globalization.CultureInfo"/>
/// in its constructor and routes every call to the correct locale-specific static class.
/// Without <c>ArbDefaultClass</c>, each .arb file produces an independent static class
/// named by PascalCasing its filename segments and appending "Localizations"
/// (e.g. <c>en.arb</c> → <c>EnLocalizations</c>), and no dispatcher is generated.
///
/// Additionally, enums decorated with <c>[ArbLocalize]</c> (on the enum or individual
/// members) get empty placeholder entries added to every .arb file so translators can
/// fill them in.  The key format is <c>&lt;EnumTypeName&gt;&lt;MemberName&gt;</c> in
/// camelCase (e.g. <c>myEnumOneYoung</c>).  A <c>Localize(EnumType)</c> overload is
/// also added to the dispatcher class so callers can write <c>l.Localize(myEnumValue)</c>.
///
/// The optional <c>description</c> argument on <c>[ArbLocalize]</c> is written into
/// the <c>@metadata</c> block of the <b>default locale</b> ARB file only.
///
/// Namespace resolution order (first non-empty wins):
/// <list type="number">
///   <item>Per-file <c>DefaultNamespace</c> metadata on the <c>&lt;ArbSource&gt;</c> item</item>
///   <item><c>ArbDefaultNamespace</c> MSBuild property</item>
///   <item><c>RootNamespace</c> MSBuild property (always set by the SDK to the project name)</item>
/// </list>
///
/// Consumer setup in their .csproj (all optional — driven by build props):
/// <code>
///   &lt;PropertyGroup&gt;
///     &lt;ArbDefaultClass&gt;AppLocale&lt;/ArbDefaultClass&gt;
///     &lt;ArbDefaultNamespace&gt;MyApp.Localizations&lt;/ArbDefaultNamespace&gt;
///     &lt;ArbDefaultLangCode&gt;en&lt;/ArbDefaultLangCode&gt;
///   &lt;/PropertyGroup&gt;
///   &lt;ItemGroup&gt;
///     &lt;ArbSource Include="arbs\app_en.arb" /&gt;
///     &lt;ArbSource Include="arbs\Czech.arb" /&gt;
///   &lt;/ItemGroup&gt;
/// </code>
/// </summary>
[Generator]
public sealed class ArbSourceGenerator : IIncrementalGenerator {
    private static readonly DiagnosticDescriptor PARSE_ERROR = new(
        id: "ARB001",
        title: "ARB parse error",
        messageFormat: "Failed to parse '{0}': {1}",
        category: "ArbGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context) {
        var arbFiles = context.AdditionalTextsProvider
            .Where(file => file.Path.EndsWith(".arb", StringComparison.OrdinalIgnoreCase));

        var allFiles = arbFiles.Combine(context.AnalyzerConfigOptionsProvider).Collect();

        // Collect enum declarations that have [ArbLocalize] on the type or its members
        var enumsWithAttribute = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is EnumDeclarationSyntax,
                transform: static (genCtx, _) => {
                    EnumDeclarationSyntax enumDecl = (EnumDeclarationSyntax)genCtx.Node;
                    INamedTypeSymbol? symbol = genCtx.SemanticModel.GetDeclaredSymbol(enumDecl) as INamedTypeSymbol;
                    if (symbol is null) return null;

                    const string ATTR_SHORT = "ArbLocalize";
                    const string ATTR_FULL = "ArbLocalizeAttribute";

                    AttributeData? enumAttr = symbol.GetAttributes()
                        .FirstOrDefault(a => a.AttributeClass?.Name is ATTR_SHORT or ATTR_FULL);

                    if (enumAttr is null) return null;

                    string? description = GetAttributeDescription(enumAttr);

                    List<string> members = symbol.GetMembers().Where(w => w.Kind == SymbolKind.Field).Select(m => m.Name).ToList();

                    if (members.Count == 0) return null;

                    return new EnumLocalizationInfo(
                        symbol.ToDisplayString(),
                        symbol.Name,
                        members.ToArray(),
                        description
                    );
                }
            )
            .Where(static info => info is not null)
            .Select(static (info, _) => info!)
            .Collect();

        // Combine ARB files and enum info into a single output pass
        var combined = allFiles.Combine(enumsWithAttribute);

        context.RegisterSourceOutput(combined, (ctx, pair) => {
            var arbPairs = pair.Left;
            var enumInfos = pair.Right;

            // ── Resolve the default lang code (same for all files in a project) ──────
            string? defaultLangCode = null;
            if (arbPairs.Length > 0) {
                AnalyzerConfigOptions globalOpts = arbPairs[0].Right.GlobalOptions;
                defaultLangCode = GetGlobalProp(globalOpts, "ArbDefaultLangCode");
            }

            // ── Step 1: inject missing ARB keys for [ArbLocalize] enums ──────────────
            if (!enumInfos.IsEmpty) {
                foreach ((AdditionalText file, AnalyzerConfigOptionsProvider _) in arbPairs) {
                    string? rawContent = file.GetText(ctx.CancellationToken)?.ToString();
                    if (string.IsNullOrWhiteSpace(rawContent)) continue;

                    ArbParseResult parseResult = new ArbParser().ParseContent(rawContent!);
                    if (!parseResult.ValidationResults.IsValid) continue;

                    ArbDocument doc = parseResult.Document!;

                    // Determine whether this file is the primary/default locale
                    string? fileLangCode = FirstNonEmpty(
                        InferLangCodeFromFilename(Path.GetFileNameWithoutExtension(file.Path)),
                        doc.Locale,
                        defaultLangCode
                    );

                    bool isDefaultLocale = !string.IsNullOrWhiteSpace(defaultLangCode)
                                           && StringHelper.NormalizeLocale(fileLangCode) == StringHelper.NormalizeLocale(defaultLangCode);

                    bool modified = false;

                    foreach (EnumLocalizationInfo info in enumInfos) {
                        string? attrDescription = info.Description;
                        foreach (string member in info.Members) {
                            string key = StringHelper.ArbKeyForEnumMember(info.SimpleName, member);

                            if (!doc.Entries.TryGetValue(key, out ArbEntry? existing)) {
                                // New entry: only write @metadata description in the default locale file
                                ArbMetadata? metadata = isDefaultLocale && attrDescription != null
                                    ? new ArbMetadata {
                                        Description = attrDescription
                                    }
                                    : null;

                                doc.Entries[key] = new ArbEntry {
                                    Key = key,
                                    Value = string.Empty,
                                    Metadata = metadata
                                };
                                modified = true;
                            }
                            else if (isDefaultLocale && attrDescription != null) {
                                // Existing entry in the default locale: backfill the description
                                // if it has none yet (never overwrite a description the translator set)
                                if (existing.Metadata?.Description == null) {
                                    if (existing.Metadata == null) {
                                        existing.Metadata = new ArbMetadata {
                                            Description = attrDescription
                                        };
                                    }
                                    else {
                                        existing.Metadata.Description = attrDescription;
                                    }
                                    modified = true;
                                }
                            }
                        }
                    }

                    if (modified) {
                        WriteArbFileToDisk(file.Path, ArbSerializer.Serialize(doc));
                    }
                }
            }

            // ── Step 2: parse ARB files and emit locale-specific C# classes ──────────
            // Key: baseClassName → list of (document, className, namespace, defaultLang)
            Dictionary<string, List<(ArbDocument Document, string ClassName, string NamespaceName, string DefaultLangCode)>> groups = new();

            foreach ((AdditionalText file, AnalyzerConfigOptionsProvider optionsProvider) in arbPairs) {
                // Re-read: the file may have been updated in Step 1
                string? content = file.GetText(ctx.CancellationToken)?.ToString();

                // Fall back to the on-disk version if the in-memory text is stale
                string? diskContent = ReadArbFileFromDisk(file.Path);
                if (!string.IsNullOrWhiteSpace(diskContent)) {
                    content = diskContent;
                }

                if (string.IsNullOrWhiteSpace(content)) continue;

                ArbParseResult result = new ArbParser().ParseContent(content!);

                if (!result.ValidationResults.IsValid) {
                    foreach (var error in result.ValidationResults.Errors) {
                        ctx.ReportDiagnostic(Diagnostic.Create(PARSE_ERROR, Location.None, file.Path, error.Keyword, error.Message, error.InstanceLocation, error.SchemaPath));
                    }
                    continue;
                }

                ArbDocument document = result.Document!;

                string? fileNameWithoutExt = Path.GetFileNameWithoutExtension(file.Path);
                AnalyzerConfigOptions fileOptions = optionsProvider.GetOptions(file);
                AnalyzerConfigOptions globalOptions = optionsProvider.GlobalOptions;

                // ── Resolve namespace ──
                string namespaceName = FirstNonEmpty(
                    GetFileMeta(fileOptions, "DefaultNamespace"),
                    GetGlobalProp(globalOptions, "ArbDefaultNamespace"),
                    GetGlobalProp(globalOptions, "RootNamespace"),
                    "Arb.Generated"
                )!;

                // ── Resolve language code ──
                string? langCode = FirstNonEmpty(
                    InferLangCodeFromFilename(fileNameWithoutExt),
                    document.Locale,
                    GetGlobalProp(globalOptions, "ArbDefaultLangCode"));

                if (!string.IsNullOrWhiteSpace(langCode)) {
                    document.Locale = langCode!;
                }

                // ── Resolve class name ──
                string className;
                string? baseClass = FirstNonEmpty(
                    GetFileMeta(fileOptions, "DefaultClass"),
                    GetGlobalProp(globalOptions, "ArbDefaultClass")
                );

                if (!string.IsNullOrWhiteSpace(baseClass)) {
                    string localeSuffix = StringHelper.NormalizeLocale(document.Locale);

                    // Locale-specific classes always get a suffix; the dispatcher owns the bare base name.
                    className = string.IsNullOrWhiteSpace(localeSuffix)
                        ? baseClass!
                        : baseClass + "_" + localeSuffix;
                }
                else {
                    className = string.Concat(Array.ConvertAll(fileNameWithoutExt.Split('_'), StringHelper.ToPascalCase))
                                + "Localizations";
                }

                // Emit the locale-specific class
                string source = new ArbCodeGenerator().GenerateClass(document, className, namespaceName);
                ctx.AddSource($"{fileNameWithoutExt}.g.cs", source);

                // Accumulate for dispatcher generation
                if (!string.IsNullOrWhiteSpace(baseClass)) {
                    string defaultLangCodeForGroup = FirstNonEmpty(
                        GetFileMeta(fileOptions, "DefaultLangCode"),
                        GetGlobalProp(globalOptions, "ArbDefaultLangCode")) ?? string.Empty;

                    if (!groups.TryGetValue(baseClass!, out List<(ArbDocument Document, string ClassName, string NamespaceName, string DefaultLangCode)>? list)) {
                        list = [];
                        groups[baseClass!] = list;
                    }
                    list.Add((document, className, namespaceName, defaultLangCodeForGroup));
                }
            }

            // ── Step 3: emit dispatcher classes (with Localize overloads if enums present) ──
            IReadOnlyList<EnumLocalizationInfo>? enumForDispatcher = enumInfos.IsEmpty
                ? null
                : (IReadOnlyList<EnumLocalizationInfo>)enumInfos
                    .Select(e => new EnumLocalizationInfo(e.FullName, e.SimpleName, e.Members, e.Description))
                    .ToList();

            foreach (var groupPair in groups) {
                string? baseClassName = groupPair.Key;
                List<(ArbDocument Document, string ClassName, string NamespaceName, string DefaultLangCode)>? entries = groupPair.Value;

                string? groupDefaultLangCode = entries.FirstOrDefault(e => !string.IsNullOrEmpty(e.DefaultLangCode)).DefaultLangCode;
                string? namespaceName = entries[0].NamespaceName;

                ArbDocument? defaultDocument = null;
                if (!string.IsNullOrWhiteSpace(groupDefaultLangCode)) {
                    string normalizedDefault = StringHelper.NormalizeLocale(groupDefaultLangCode);
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
                    namespaceName,
                    enumForDispatcher
                );

                ctx.AddSource($"{baseClassName}Dispatcher.g.cs", dispatcher);
            }
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Extracts the first constructor argument (the description string) from an
    /// <c>[ArbLocalize("...")]</c> attribute, or <c>null</c> if absent or empty.
    /// </summary>
    private static string? GetAttributeDescription(AttributeData? attr) {
        if (attr is null) return null;
        if (attr.ConstructorArguments.Length == 0) return null;

        object? value = attr.ConstructorArguments[0].Value;
        string? str = value as string;
        return string.IsNullOrWhiteSpace(str) ? null : str;
    }

    /// <summary>
    /// Writes ARB content to disk. Wrapped in its own method so that the RS1035
    /// pragma suppression is scoped as narrowly as possible.
    /// </summary>
#pragma warning disable RS1035
    private static void WriteArbFileToDisk(string path, string content) {
        try {
            File.WriteAllText(path, content, System.Text.Encoding.UTF8);
        }
        catch {
            // Best-effort; don't crash the build if the file is read-only or locked
        }
    }

    private static string? ReadArbFileFromDisk(string path) {
        try {
            return File.ReadAllText(path, System.Text.Encoding.UTF8);
        }
        catch {
            return null;
        }
    }
#pragma warning restore RS1035

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