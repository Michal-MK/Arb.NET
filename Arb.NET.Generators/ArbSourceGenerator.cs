using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Arb.NET.Models;

namespace Arb.NET.Generators;

/// <summary>
/// Roslyn incremental source generator that turns .arb files into strongly-typed C# localization classes.
///
/// Configuration is read from an <c>l10n.yaml</c> file in the project root (auto-included as an
/// AdditionalFile by the NuGet-shipped .targets file — no .csproj changes required).
///
/// <code>
/// arb-dir: arbs
/// template-arb-file: app_en.arb
/// output-class: AppLocale
/// output-namespace: MyApp.Localizations
/// </code>
///
/// When <c>output-class</c> is set (e.g. <c>AppLocale</c>), locale-specific static
/// classes are generated with that name as a prefix (e.g. <c>AppLocale_en</c>,
/// <c>AppLocale_cs</c>), and a dispatcher class using the bare base name (<c>AppLocale</c>)
/// is also generated.  The dispatcher accepts a <see cref="System.Globalization.CultureInfo"/>
/// in its constructor and routes every call to the correct locale-specific static class.
/// Without <c>output-class</c>, each .arb file produces an independent static class
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
/// the <c>@metadata</c> block of the default locale ARB file only.
///
/// Namespace resolution order (first non-empty wins):
/// <list type="number">
///   <item><c>output-namespace</c> in l10n.yaml</item>
///   <item><c>RootNamespace</c> in the owning project file</item>
///   <item>Project <c>AssemblyName</c> or project filename</item>
///   <item><c>Arb.Generated</c></item>
/// </list>
/// </summary>
[Generator]
public sealed class ArbSourceGenerator : IIncrementalGenerator {
    private static readonly DiagnosticDescriptor GENERATION_ERROR = new(
        id: "ARB001",
        title: "ARB generation error",
        messageFormat: "{0}",
        category: "ArbGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context) {
        var arbFiles = context.AdditionalTextsProvider
            .Where(file => file.Path.EndsWith(Constants.ARB_FILE_EXT, StringComparison.OrdinalIgnoreCase));

        var configFiles = context.AdditionalTextsProvider
            .Where(file => file.Path.EndsWith(Constants.LOCALIZATION_FILE, StringComparison.OrdinalIgnoreCase));

        // Collect all .arb files and the (at most one) l10n.yaml together.
        var allFilesAndConfig = arbFiles.Collect()
            .Combine(configFiles.Collect())
            .Combine(context.AnalyzerConfigOptionsProvider);

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

                    return new EnumLocalizationInfo {
                        FullName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        SimpleName = symbol.Name,
                        Members = members,
                        Description = description
                    };
                }
            )
            .Where(static info => info is not null)
            .Select(static (info, _) => info!)
            .Collect();

        // Combine everything into a single output pass
        var combined = allFilesAndConfig.Combine(enumsWithAttribute);

        context.RegisterSourceOutput(combined, (ctx, pair) => {
            var ((arbPairs, configTexts), globalOptionsProvider) = pair.Left;
            var enumInfos = pair.Right;

            if (configTexts.IsEmpty) return;

            IReadOnlyList<EnumLocalizationInfo>? enumForDispatcher = enumInfos.IsEmpty
                ? null
                : enumInfos
                    .Select(e => new EnumLocalizationInfo {
                        FullName = e.FullName,
                        SimpleName = e.SimpleName,
                        Members = e.Members,
                        Description = e.Description
                    })
                    .ToList();

            // ── Process each l10n.yaml independently ─────────────────────────────────────
            foreach (AdditionalText configText in configTexts) {
                string? yaml = configText.GetText(ctx.CancellationToken)?.ToString();
                if (string.IsNullOrWhiteSpace(yaml)) continue;

                L10nConfig config = L10nConfig.Parse(yaml!);
                string configDir = Path.GetDirectoryName(configText.Path)!;

                // ── Filter .arb files to this config's arb-dir ───────────────────────────
                string arbDirAbsolute = Path.GetFullPath(Path.Combine(configDir, config.ArbDir))
                                            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                        + Path.DirectorySeparatorChar;

                List<AdditionalText> relevantArbs = arbPairs
                    .Where(f => f.Path.StartsWith(arbDirAbsolute, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                // ── Determine the default lang code from the template file ────────────────
                string? defaultLangCode = ResolveDefaultLangCode(config, relevantArbs, ctx.CancellationToken);

                // ── Step 1: inject missing ARB keys for [ArbLocalize] enums ──────────────
                if (!enumInfos.IsEmpty) {
                    foreach (AdditionalText file in relevantArbs) {
                        string? rawContent = file.GetText(ctx.CancellationToken)?.ToString();
                        if (string.IsNullOrWhiteSpace(rawContent)) continue;

                        ArbParseResult parseResult = new ArbParser().ParseContent(rawContent!);
                        if (!parseResult.ValidationResults.IsValid) continue;

                        ArbDocument doc = parseResult.Document!;

                        string? fileLangCode = StringHelper.FirstNonEmpty(
                            StringHelper.InferLangCodeFromFilename(Path.GetFileNameWithoutExtension(file.Path)),
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

                string namespaceName = ArbGeneration.ResolveNamespaceName(
                    config,
                    GetGlobalProp(globalOptionsProvider.GlobalOptions, "RootNamespace"),
                    GetGlobalProp(globalOptionsProvider.GlobalOptions, "AssemblyName"),
                    StringHelper.FirstNonEmpty(
                        GetGlobalProp(globalOptionsProvider.GlobalOptions, "MSBuildProjectName"),
                        Path.GetFileName(configDir)));

                string outputDir = Path.Combine(configDir, config.ArbDir);
                string? baseClass = string.IsNullOrWhiteSpace(config.OutputClass) ? null : config.OutputClass;
                List<ArbGeneration.Input> generationInputs = [];

                foreach (AdditionalText file in relevantArbs) {
                    string? content = ReadArbFileFromDisk(file.Path)
                                      ?? file.GetText(ctx.CancellationToken)?.ToString();
                    if (string.IsNullOrWhiteSpace(content)) {
                        continue;
                    }

                    ArbParseResult parseResult = new ArbParser().ParseContent(content!);
                    if (!parseResult.ValidationResults.IsValid) {
                        foreach (ArbValidationError error in parseResult.ValidationResults.Errors) {
                            ctx.ReportDiagnostic(Diagnostic.Create(
                                GENERATION_ERROR,
                                Location.None,
                                $"Failed to parse '{file.Path}': [{error.Keyword}] {error.Message}"));
                        }

                        continue;
                    }

                    generationInputs.Add(new ArbGeneration.Input(
                        Path.GetFileNameWithoutExtension(file.Path),
                        parseResult.Document!));
                }

                foreach (ArbGeneration.Output output in ArbGeneration.GenerateFiles(
                             generationInputs,
                             namespaceName,
                             baseClass,
                             defaultLangCode,
                             enumForDispatcher)) {
                    WriteGeneratedFile(outputDir, output.FileName, output.Content);
                }
            }
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────────

    private static string? GetAttributeDescription(AttributeData? attr) {
        if (attr is null) return null;
        if (attr.ConstructorArguments.Length == 0) return null;

        object? value = attr.ConstructorArguments[0].Value;
        string? str = value as string;
        return string.IsNullOrWhiteSpace(str) ? null : str;
    }

#pragma warning disable RS1035
    private static void WriteGeneratedFile(string outputDir, string fileName, string content) {
        try {
            Directory.CreateDirectory(outputDir);
            File.WriteAllText(Path.Combine(outputDir, fileName), content, Constants.UTF8_NO_BOM);
        }
        catch {
            // Best-effort; don't crash the build if the file cannot be written.
        }
    }

    private static void WriteArbFileToDisk(string path, string content) {
        try {
            File.WriteAllText(path, content, Constants.UTF8_NO_BOM);
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

    private static string? ResolveDefaultLangCode(L10nConfig config, IReadOnlyList<AdditionalText> relevantArbs, CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(config.TemplateArbFile)) {
            return null;
        }

        AdditionalText? templateFile = relevantArbs.FirstOrDefault(f =>
            string.Equals(Path.GetFileName(f.Path), config.TemplateArbFile, StringComparison.OrdinalIgnoreCase));

        if (templateFile == null) {
            return null;
        }

        string? templateContent = templateFile.GetText(cancellationToken)?.ToString();
        if (!string.IsNullOrWhiteSpace(templateContent)) {
            ArbParseResult templateParse = new ArbParser().ParseContent(templateContent!);
            if (templateParse.ValidationResults.IsValid && templateParse.Document?.Locale is not null) {
                return templateParse.Document.Locale;
            }
        }

        return StringHelper.InferLangCodeFromFilename(Path.GetFileNameWithoutExtension(templateFile.Path));
    }
    private static string? GetGlobalProp(AnalyzerConfigOptions options, string key) {
        options.TryGetValue($"build_property.{key}", out string? value);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}