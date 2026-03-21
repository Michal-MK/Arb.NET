using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Arb.NET.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ArbDispatcherCodeFix)), Shared]
public sealed class ArbDispatcherCodeFix : CodeFixProvider {
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(ArbDispatcherAnalyzer.DIAGNOSTIC_ID);

    public override FixAllProvider? GetFixAllProvider() => null;

    public override Task RegisterCodeFixesAsync(CodeFixContext context) {
        Diagnostic diagnostic = context.Diagnostics[0];
        if (!diagnostic.Properties.TryGetValue(ArbDispatcherAnalyzer.KEY_PROPERTY, out string? keyName) ||
            string.IsNullOrEmpty(keyName)) {
            return Task.CompletedTask;
        }

        string? filePath = context.Document.FilePath;
        if (string.IsNullOrEmpty(filePath)) {
            return Task.CompletedTask;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Generate ARB key '{keyName}'",
                createChangedSolution: ct => GenerateKeyAsync(context.Document.Project.Solution, filePath!, keyName!, ct),
                equivalenceKey: $"GenerateArbKey:{keyName}"
            ),
            diagnostic
        );

        return Task.CompletedTask;
    }

    private static Task<Solution> GenerateKeyAsync(Solution solution, string documentPath, string memberName, CancellationToken cancellationToken) {
        string arbKey = StringHelper.ToCamelCase(memberName);

        string? projectDir = FindProjectDir(documentPath);
        if (projectDir == null)
            return Task.FromResult(solution);

        string arbDir = ResolveArbDirectory(projectDir);

        foreach (string arbFilePath in Directory.EnumerateFiles(arbDir, Constants.ANY_ARB)) {
            cancellationToken.ThrowIfCancellationRequested();

            try {
                string oldContent = File.ReadAllText(arbFilePath);
                string? newContent = TryAddKey(oldContent, arbKey);
                if (newContent == null) continue;
                File.WriteAllText(arbFilePath, newContent, Constants.UTF8_NO_BOM);
            }
            catch {
                // TODO Handle
            }
        }

        return Task.FromResult(solution);
    }

    /// <summary>
    /// Walks up from <paramref name="filePath"/> until a directory containing
    /// <c>l10n.yaml</c> is found. Returns null if not found within 10 levels.
    /// </summary>
    private static string? FindProjectDir(string filePath) {
        string? dir = Path.GetDirectoryName(filePath);
        for (int i = 0; i < 10 && dir != null; i++) {
            if (File.Exists(Path.Combine(dir, Constants.LOCALIZATION_FILE)))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    /// <summary>
    /// Reads <c>arb-dir:</c> from <c>l10n.yaml</c> in <paramref name="projectDir"/>.
    /// Falls back to <paramref name="projectDir"/> if absent or unreadable.
    /// </summary>
    private static string ResolveArbDirectory(string projectDir) {
        try {
            string localization = Path.Combine(projectDir, Constants.LOCALIZATION_FILE);
            if (!File.Exists(localization)) return projectDir;

            string? line = File.ReadLines(localization)
                .Select(l => l.Trim().TrimStart('\uFEFF'))
                .FirstOrDefault(l => l.StartsWith("arb-dir:"));
            if (line == null) return projectDir;

            string configured = line.Substring("arb-dir:".Length).Trim().Trim('"', '\'');
            if (string.IsNullOrEmpty(configured)) return projectDir;

            string arbDir = Path.Combine(projectDir, configured);
            return Directory.Exists(arbDir) ? arbDir : projectDir;
        }
        catch {
            return projectDir;
        }
    }

    /// <summary>
    /// Returns the updated content with <paramref name="arbKey"/> inserted, or
    /// <c>null</c> if the key is already present or the file cannot be parsed.
    /// Uses <see cref="ArbParser"/> and <see cref="ArbSerializer"/> so the output
    /// is sorted and formatted identically to every other ARB write in this project.
    /// </summary>
    private static string? TryAddKey(string content, string arbKey) {
        ArbParseResult parsed = new ArbParser().ParseContent(content);
        if (parsed.Document == null) return null;
        if (parsed.Document.Entries.ContainsKey(arbKey)) return null;

        parsed.Document.Entries[arbKey] = new ArbEntry {
            Key = arbKey,
            Value = string.Empty
        };
        return ArbSerializer.Serialize(parsed.Document);
    }
}