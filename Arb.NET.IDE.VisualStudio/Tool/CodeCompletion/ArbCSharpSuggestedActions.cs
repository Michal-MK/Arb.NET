using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Arb.NET.IDE.Common.Services;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Arb.NET.IDE.VisualStudio.Tool.CodeCompletion;

[Export(typeof(ISuggestedActionsSourceProvider))]
[Name("ArbGenerateCSharpKey")]
[ContentType("CSharp")]
[TextViewRole(PredefinedTextViewRoles.Editable)]
internal sealed class ArbCSharpSuggestedActionsSourceProvider : ISuggestedActionsSourceProvider {
    public ISuggestedActionsSource? CreateSuggestedActionsSource(ITextView textView, ITextBuffer textBuffer) {
        return new ArbCSharpSuggestedActionsSource(textView, textBuffer);
    }
}

internal sealed class ArbCSharpSuggestedActionsSource(ITextView textView, ITextBuffer textBuffer) : ISuggestedActionsSource {
    // Matches a member access on any identifier: SomeType.SomeKey or someVar.SomeKey
    // Group 1 = the member name (PascalCase assumed for ARB keys)
    private static readonly Regex MEMBER_ACCESS_REGEX = new(@"\b\w+\.([A-Z]\w*)\b", RegexOptions.Compiled);

    public event EventHandler<EventArgs>? SuggestedActionsChanged;

    public bool TryGetTelemetryId(out Guid telemetryId) {
        telemetryId = Guid.Empty;
        return false;
    }

    public Task<bool> HasSuggestedActionsAsync(
        ISuggestedActionCategorySet requestedActionCategories,
        SnapshotSpan range,
        CancellationToken cancellationToken
    ) {
        return Task.FromResult(TryGetUnknownArbKeyInRange(range, out _, out _));
    }

    public IEnumerable<SuggestedActionSet> GetSuggestedActions(
        ISuggestedActionCategorySet requestedActionCategories,
        SnapshotSpan range, CancellationToken cancellationToken
    ) {
        if (!TryGetUnknownArbKeyInRange(range, out string? key, out string? projectDir) || key == null || projectDir == null)
            yield break;

        yield return new SuggestedActionSet(
            null,
            [
                new GenerateAndOpenArbKeySuggestedAction(key, projectDir)
            ],
            null,
            SuggestedActionSetPriority.High,
            range
        );
    }

    /// <summary>
    /// Scans <paramref name="range"/> for a member-access expression whose member name is an
    /// unknown ARB key on an <c>IArbLocale</c> dispatcher type.
    /// Background-thread-safe: uses only snapshot text and <see cref="ArbKeyService"/>.
    /// </summary>
    private bool TryGetUnknownArbKeyInRange(SnapshotSpan range, out string? key, out string? projectDir) {
        key = null;
        projectDir = null;

        ITextBuffer documentBuffer = textView.TextDataModel.DocumentBuffer;
        if (!documentBuffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document) &&
            !textBuffer.Properties.TryGetProperty(typeof(ITextDocument), out document)) return false;

        string? dir = ArbKeyService.FindProjectDirFromFilePath(document.FilePath);
        if (string.IsNullOrWhiteSpace(dir)) return false;

        HashSet<string> validKeys = ArbKeyService.GetKeys(dir!)
            .Select(k => k.Key)
            .ToHashSet(StringComparer.Ordinal);

        if (validKeys.Count == 0) return false;

        ITextSnapshot snapshot = range.Snapshot;
        int startLine = snapshot.GetLineNumberFromPosition(range.Start.Position);
        int endLine = snapshot.GetLineNumberFromPosition(range.End.Position);

        for (int i = startLine; i <= endLine; i++) {
            ITextSnapshotLine line = snapshot.GetLineFromLineNumber(i);
            string lineText = line.GetText();

            foreach (Match match in MEMBER_ACCESS_REGEX.Matches(lineText)) {
                string candidate = match.Groups[1].Value;
                if (validKeys.Contains(candidate)) continue;

                int memberStart = line.Start.Position + match.Groups[1].Index;
                SnapshotSpan memberSpan = new(snapshot, new Span(memberStart, match.Groups[1].Length));
                if (!range.IntersectsWith(memberSpan)) continue;

                key = candidate;
                projectDir = dir!;
                return true;
            }
        }

        return false;
    }

    public void Dispose() { }
}

internal sealed class GenerateAndOpenArbKeySuggestedAction(string key, string projectDir) : ISuggestedAction {

    public string DisplayText => $"Generate ARB key '{key}' and open editor";
    public string? IconAutomationText => null;
    public string? InputGestureText => null;
    public bool HasActionSets => false;
    public bool HasPreview => false;
    public System.Windows.Media.ImageSource? IconSource => null;
    public Microsoft.VisualStudio.Imaging.Interop.ImageMoniker IconMoniker => default;

    public Task<IEnumerable<SuggestedActionSet>> GetActionSetsAsync(CancellationToken cancellationToken) {
        return Task.FromResult(Enumerable.Empty<SuggestedActionSet>());
    }

    public Task<object> GetPreviewAsync(CancellationToken cancellationToken) {
        return Task.FromResult<object>(string.Empty);
    }

    public bool TryGetTelemetryId(out Guid telemetryId) {
        telemetryId = Guid.Empty;
        return false;
    }

    public void Invoke(CancellationToken cancellationToken) {
        string arbKey = StringHelper.ToCamelCase(key);
        string arbDir = LocalizationYamlService.ResolveArbDirectory(projectDir);

        foreach (string arbFilePath in Directory.EnumerateFiles(arbDir, "*.arb")) {
            try {
                string content = File.ReadAllText(arbFilePath);
                ArbParseResult parsed = new ArbParser().ParseContent(content);
                if (parsed.Document == null) continue;
                if (parsed.Document.Entries.ContainsKey(arbKey)) continue;

                parsed.Document.Entries[arbKey] = new ArbEntry {
                    Key = arbKey,
                    Value = ""
                };
                File.WriteAllText(arbFilePath, ArbSerializer.Serialize(parsed.Document));
            }
            catch {
                // ignored
            }
        }

        ArbKeyService.InvalidateCache(projectDir);
        ArbXamlValidationTagger.InvalidateAll();

        string? templatePath = ArbKeyService.GetKeys(projectDir).FirstOrDefault()?.ArbFilePath;
        if (templatePath != null)
            ArbNavigation.OpenToolWindowAtKey(templatePath, arbKey);
    }

    public void Dispose() { }
}