using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Arb.NET.Analyzers;

/// <summary>
/// Reports <see cref="DIAGNOSTIC_ID"/> (ARB002) when code accesses a member on an
/// This complements the Roslyn CS1061 error with an actionable ARB-specific diagnostic
/// that a <see cref="ArbDispatcherCodeFix"/> can act on.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ArbDispatcherAnalyzer : DiagnosticAnalyzer
{
    public const string DIAGNOSTIC_ID = "ARB002";

    /// <summary>Property bag key used to pass the key name to the code fix provider.</summary>
    internal const string KEY_PROPERTY = "key";

    private static readonly DiagnosticDescriptor RULE = new(
        DIAGNOSTIC_ID,
        title: "Unknown ARB key",
        messageFormat: "'{0}' is not a known ARB key on '{1}'",
        category: "Arb.NET",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The member accessed does not correspond to any key defined in the .arb files. " +
                     "Use 'Generate ARB key' to create it."
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(RULE);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
    }

    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
    {
        var access = (MemberAccessExpressionSyntax)context.Node;
        string memberName = access.Name.Identifier.Text;

        TypeInfo typeInfo = context.SemanticModel.GetTypeInfo(access.Expression, context.CancellationToken);
        ITypeSymbol? receiverType = typeInfo.Type;
        if (receiverType is null or IErrorTypeSymbol) return;

        // Only fire on types that implement IArbLocale
        INamedTypeSymbol? iArbLocale = context.Compilation.GetTypeByMetadataName("Arb.NET.IArbLocale");
        if (iArbLocale == null) return;

        bool implementsInterface = receiverType.AllInterfaces.Contains(iArbLocale, SymbolEqualityComparer.Default);
        if (!implementsInterface) return;

        // If the member is defined, no diagnostic needed
        ImmutableArray<ISymbol> members = receiverType.GetMembers(memberName);
        if (!members.IsEmpty) return;

        ImmutableDictionary<string, string?> properties = ImmutableDictionary<string, string?>.Empty
            .Add(KEY_PROPERTY, memberName);

        Diagnostic diagnostic = Diagnostic.Create(
            RULE,
            access.Name.GetLocation(),
            properties,
            memberName,
            receiverType.Name);

        context.ReportDiagnostic(diagnostic);
    }
}