using Arb.NET.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Arb.NET.Tool;

internal static class EnumScanner {
    private const string ATTR_SHORT = "ArbLocalize";
    private const string ATTR_FULL = "ArbLocalizeAttribute";

    /// <summary>
    /// Walks all .cs files under <paramref name="projectDir"/> (excluding obj/ and bin/) and
    /// returns one <see cref="EnumLocalizationInfo"/> for every enum decorated with
    /// <c>[ArbLocalize]</c> or <c>[ArbLocalizeAttribute]</c>.
    /// </summary>
    internal static IReadOnlyList<EnumLocalizationInfo> Scan(string projectDir) {
        List<EnumLocalizationInfo> result = [];

        foreach (string file in EnumerateCsFiles(projectDir)) {
            string source = File.ReadAllText(file, Constants.UTF8_NO_BOM);
            SyntaxTree tree = CSharpSyntaxTree.ParseText(source);
            CompilationUnitSyntax root = (CompilationUnitSyntax)tree.GetRoot();

            foreach (EnumDeclarationSyntax enumDecl in root.DescendantNodes().OfType<EnumDeclarationSyntax>()) {
                AttributeSyntax? attr = FindArbLocalizeAttribute(enumDecl);
                if (attr is null) continue;

                List<string> members = enumDecl.Members
                    .Select(m => m.Identifier.Text)
                    .ToList();

                if (members.Count == 0) continue;

                string simpleName = enumDecl.Identifier.Text;
                string fullName = BuildFullName(enumDecl);
                string? description = ExtractDescriptionArgument(attr);

                result.Add(new EnumLocalizationInfo {
                    FullName = fullName,
                    SimpleName = simpleName,
                    Members = members,
                    Description = description
                });
            }
        }

        return result;
    }

    private static AttributeSyntax? FindArbLocalizeAttribute(EnumDeclarationSyntax enumDecl) {
        foreach (AttributeListSyntax attrList in enumDecl.AttributeLists) {
            foreach (AttributeSyntax attr in attrList.Attributes) {
                string name = attr.Name.ToString();
                // Strip namespace qualifiers if present
                int dot = name.LastIndexOf('.');
                if (dot >= 0) name = name[(dot + 1)..];

                if (name is ATTR_SHORT or ATTR_FULL) return attr;
            }
        }
        return null;
    }

    private static string? ExtractDescriptionArgument(AttributeSyntax attr) {
        AttributeArgumentSyntax? first = attr.ArgumentList?.Arguments.FirstOrDefault();
        if (first is null) return null;

        if (first.Expression is LiteralExpressionSyntax literal &&
            literal.IsKind(SyntaxKind.StringLiteralExpression)) {
            string value = literal.Token.ValueText;
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        return null;
    }

    private static string BuildFullName(EnumDeclarationSyntax enumDecl) {
        List<string> parts = [enumDecl.Identifier.Text];

        SyntaxNode? current = enumDecl.Parent;
        while (current is not null) {
            switch (current) {
                case TypeDeclarationSyntax type:
                    parts.Add(type.Identifier.Text);
                    current = current.Parent;
                    break;
                case NamespaceDeclarationSyntax ns:
                    parts.Add(ns.Name.ToString());
                    current = current.Parent;
                    break;
                case FileScopedNamespaceDeclarationSyntax fsns:
                    parts.Add(fsns.Name.ToString());
                    current = current.Parent;
                    break;
                default:
                    current = current.Parent;
                    break;
            }
        }

        parts.Reverse();
        return "global::" + string.Join(".", parts);
    }

    private static IEnumerable<string> EnumerateCsFiles(string projectDir) {
        return Directory.EnumerateFiles(projectDir, "*.cs", SearchOption.AllDirectories)
            .Where(static f => {
                string norm = f.Replace('\\', '/');
                return !norm.Contains("/obj/") && !norm.Contains("/bin/");
            });
    }
}
