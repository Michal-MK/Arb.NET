using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Arb.NET;
using Arb.NET.IDE.Common.Services;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Arb.NET.IDE.VisualStudio.Tool.CodeCompletion;

[Export(typeof(ISuggestedActionsSourceProvider))]
[Name("ArbGenerateKey")]
[ContentType("xaml")]
[TextViewRole(PredefinedTextViewRoles.Editable)]
internal sealed class ArbXamlSuggestedActionsSourceProvider : ISuggestedActionsSourceProvider {
    public ISuggestedActionsSource CreateSuggestedActionsSource(ITextView textView, ITextBuffer textBuffer) {
        return new ArbXamlSuggestedActionsSource(textView, textBuffer);
    }
}

internal sealed class ArbXamlSuggestedActionsSource(ITextView textView, ITextBuffer textBuffer) : ISuggestedActionsSource {

    public event EventHandler<EventArgs>? SuggestedActionsChanged;

    public bool TryGetTelemetryId(out Guid telemetryId) {
        telemetryId = Guid.Empty;
        return false;
    }

    public Task<bool> HasSuggestedActionsAsync(
        ISuggestedActionCategorySet requestedActionCategories,
        SnapshotSpan range, CancellationToken cancellationToken
    ) {
        return Task.FromResult(TryGetUnknownArbKeyInRange(range, out _, out _));
    }

    public IEnumerable<SuggestedActionSet> GetSuggestedActions(
        ISuggestedActionCategorySet requestedActionCategories,
        SnapshotSpan range, CancellationToken cancellationToken
    ) {
        if (!TryGetUnknownArbKeyInRange(range, out string? key, out string? projectDir) || key == null || projectDir == null) {
            yield break;
        }

        yield return new SuggestedActionSet(
            null,
            [
                new GenerateArbKeySuggestedAction(key, projectDir)
            ],
            null,
            SuggestedActionSetPriority.High,
            range
        );
    }

    /// <summary>
    /// Scans <paramref name="range"/> for unknown ARB key references.
    /// Background-thread-safe: uses only snapshot text and file I/O, no UI thread APIs.
    /// </summary>
    private bool TryGetUnknownArbKeyInRange(SnapshotSpan range, out string? key, out string? projectDir) {
        key = null;
        projectDir = null;

        // Determine the document path from the text buffer (property lookup is thread-safe).
        ITextBuffer documentBuffer = textView.TextDataModel.DocumentBuffer;
        if (!textBuffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document) &&
            !documentBuffer.Properties.TryGetProperty(typeof(ITextDocument), out document)) {
            return false;
        }

        string? dir = ArbKeyService.FindProjectDirFromFilePath(document.FilePath);
        if (string.IsNullOrWhiteSpace(dir)) return false;

        HashSet<string> validKeys = ArbKeyService.GetKeys(dir!)
            .Select(k => k.Key)
            .ToHashSet(StringComparer.Ordinal);

        // Scan every line that overlaps the range for unknown ARB key references.
        ITextSnapshot snapshot = range.Snapshot;
        int startLine = snapshot.GetLineNumberFromPosition(range.Start.Position);
        int endLine = snapshot.GetLineNumberFromPosition(range.End.Position);

        for (int lineIndex = startLine; lineIndex <= endLine; lineIndex++) {
            ITextSnapshotLine line = snapshot.GetLineFromLineNumber(lineIndex);
            string lineText = line.GetText();

            foreach (System.Text.RegularExpressions.Match match in ArbXamlContext.ARB_KEY_REGEX.Matches(lineText)) {
                string candidate = match.Groups[1].Value;
                if (validKeys.Contains(candidate)) continue;

                // Confirm the key's span overlaps the requested range
                int keyStart = line.Start.Position + match.Groups[1].Index;
                SnapshotSpan keySpan = new(snapshot, new Span(keyStart, match.Groups[1].Length));
                if (!range.IntersectsWith(keySpan)) continue;

                key = candidate;
                projectDir = dir!;
                return true;
            }
        }

        return false;
    }

    public void Dispose() { }
}

internal sealed class GenerateArbKeySuggestedAction(string key, string projectDir) : ISuggestedAction {

    public string DisplayText => $"Generate ARB key '{key}'";
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
        // camelCase the PascalCase key for the .arb file
        string arbKey = StringHelper.ToCamelCase(key);
        string arbDir = LocalizationYamlService.ResolveArbDirectory(projectDir);

        // Add the empty key to all .arb files in the directory
        foreach (string arbFilePath in Directory.EnumerateFiles(arbDir, Constants.ANY_ARB)) {
            try {
                string content = File.ReadAllText(arbFilePath);
                ArbParseResult parsed = new ArbParser().ParseContent(content);
                if (parsed.Document == null) continue;
                if (parsed.Document.Entries.ContainsKey(arbKey)) continue;

                parsed.Document.Entries[arbKey] = new ArbEntry {
                    Key = arbKey,
                    Value = string.Empty
                };
                File.WriteAllText(arbFilePath, ArbSerializer.Serialize(parsed.Document));
            }
            catch {
                // Best-effort; continue with remaining files
            }
        }

        // Invalidate ArbKeyService cache and force taggers to re-evaluate immediately
        ArbKeyService.InvalidateCache(projectDir);
        ArbXamlValidationTagger.InvalidateAll();

        // Open the ARB editor tool window pre-filtered to the new key
        string? templatePath = ArbKeyService.GetKeys(projectDir).FirstOrDefault()?.ArbFilePath;
        if (templatePath != null) {
            ArbNavigation.OpenToolWindowAtKey(templatePath, arbKey);
        }
    }

    public void Dispose() { }
}