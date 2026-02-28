using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text.RegularExpressions;
using Arb.NET.IDE.Common.Models;
using Arb.NET.IDE.Common.Services;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace Arb.NET.IDE.VisualStudio.Tool.CodeCompletion;

[Export(typeof(ICompletionSourceProvider))]
[ContentType("xaml")]
[Order(After = "default")]
[Name("ArbXamlCompletionProvider")]
public class ArbXamlCompletionSourceProvider : ICompletionSourceProvider {
    public ICompletionSource TryCreateCompletionSource(ITextBuffer textBuffer) {
        return textBuffer.Properties.GetOrCreateSingletonProperty(() => new ArbXamlCompletionSource(textBuffer));
    }
}

public class ArbXamlCompletionSource(ITextBuffer textBuffer) : ICompletionSource {
    //  // TODO(naive) Fails with global implicit xaml namespaces and probably more...
    private static readonly Regex ARB_PREFIX_PATTERN = new(@"\{[^:}]+:Arb\s+(\w*)$", RegexOptions.Compiled);

    public void AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets) {
        ITextBuffer activeBuffer = session.TextView.TextDataModel.DocumentBuffer;

        SnapshotPoint? caretPoint = session.TextView.Caret.Position.Point.GetPoint(
            activeBuffer,
            session.TextView.Caret.Position.Affinity
        );

        if (!caretPoint.HasValue) {
            SnapshotPoint? triggerPoint = session.GetTriggerPoint(activeBuffer.CurrentSnapshot);
            if (!triggerPoint.HasValue) return;
            caretPoint = triggerPoint.Value;
        }

        SnapshotPoint point = caretPoint.Value;

        ITextSnapshotLine line = point.GetContainingLine();
        string textBeforeCaret = point.Snapshot.GetText(line.Start.Position, point.Position - line.Start.Position);

        Match match = ARB_PREFIX_PATTERN.Match(textBeforeCaret);
        if (!match.Success) return;

        // TODO(naive) This is naive and may produce false positives.
        // We are inside {ext:Arb ...}: suppress default XAML completion sets.
        completionSets.Clear();

        string typedKey = match.Groups[1].Value;
        string? projectDir = TryFindProjectDirectory(activeBuffer, session.TextView.TextBuffer, textBuffer);
        if (string.IsNullOrWhiteSpace(projectDir)) return;

        List<ArbKeyInfo> keyInfos = ArbKeyService.GetKeys(projectDir!);
        if (keyInfos.Count == 0) return;

        List<Completion> completions = keyInfos
            .Where(it => it.Key.StartsWith(typedKey, System.StringComparison.OrdinalIgnoreCase))
            .Select(it => new Completion(
                        it.Key,
                        it.Key,
                        BuildDescription(it),
                        null,
                        null
                    ))
            .ToList();

        if (completions.Count == 0) return;

        Group keyGroup = match.Groups[1];
        int replacementStart = line.Start.Position + keyGroup.Index;
        int replacementLength = keyGroup.Length;
        ITrackingSpan applicableTo = point.Snapshot.CreateTrackingSpan(
            new Span(replacementStart, replacementLength), SpanTrackingMode.EdgeInclusive
        );

        completionSets.Add(
            new CompletionSet(
                "ArbXaml",
                "ARB Keys",
                applicableTo,
                completions,
                null
            )
        );
    }

    private static string? TryFindProjectDirectory(params ITextBuffer[] buffers) {
        foreach (ITextBuffer buffer in buffers.Where(b => b != null)) {
            if (!buffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document)) continue;

            string? projectDir = ArbKeyService.FindProjectDirFromFilePath(document.FilePath);
            if (!string.IsNullOrWhiteSpace(projectDir)) {
                return projectDir;
            }
        }

        return null;
    }

    private static string BuildDescription(ArbKeyInfo item) {
        List<string> parts = [];
        if (item.IsParametric) {
            parts.Add("⚠ parametric");
        }

        if (!string.IsNullOrWhiteSpace(item.Description)) {
            parts.Add(item.Description!);
        }

        return parts.Count == 0 ? "ARB key" : "ARB key — " + string.Join(" | ", parts);
    }

    public void Dispose() { }
}
