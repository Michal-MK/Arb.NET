using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text.RegularExpressions;
using Arb.NET.IDE.Common.Services;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace Arb.NET.IDE.VisualStudio.Tool.CodeCompletion;

[Export(typeof(IViewTaggerProvider))]
[ContentType("xaml")]
[TagType(typeof(IErrorTag))]
[TextViewRole(PredefinedTextViewRoles.Editable)]
[Name("ArbXamlValidationTaggerProvider")]
internal sealed class ArbXamlValidationTaggerProvider : IViewTaggerProvider
{
    public ITagger<T>? CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
    {
        if (typeof(T) != typeof(IErrorTag) && typeof(T) != typeof(ErrorTag)) {
            return null;
        }

        return buffer.Properties.GetOrCreateSingletonProperty(
            () => new ArbXamlValidationTagger(textView, buffer)) as ITagger<T>;
    }
}

internal sealed class ArbXamlValidationTagger : ITagger<ErrorTag>
{
    private readonly ITextView textView;
    private readonly ITextBuffer textBuffer;

    public ArbXamlValidationTagger(ITextView textView, ITextBuffer textBuffer)
    {
        this.textView = textView;
        this.textBuffer = textBuffer;
        this.textBuffer.Changed += OnBufferChanged;
    }

    public event EventHandler<SnapshotSpanEventArgs>? TagsChanged;

    public IEnumerable<ITagSpan<ErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans)
    {
        if (spans.Count == 0) yield break;

        ITextBuffer documentBuffer = textView.TextDataModel.DocumentBuffer;
        if (!documentBuffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document) &&
            !textBuffer.Properties.TryGetProperty(typeof(ITextDocument), out document)) {
            yield break;
        }

        string? projectDir = ArbKeyService.FindProjectDirFromFilePath(document.FilePath);
        if (string.IsNullOrWhiteSpace(projectDir)) {
            yield break;
        }

        HashSet<string> validKeys = ArbKeyService.GetKeys(projectDir!)
            .Select(it => it.Key)
            .ToHashSet(StringComparer.Ordinal);

        foreach (SnapshotSpan span in spans) {
            foreach (ITagSpan<ErrorTag> tag in GetUnknownKeyTagsInSpan(span, validKeys)) {
                yield return tag;
            }
        }
    }

    private static IEnumerable<ITagSpan<ErrorTag>> GetUnknownKeyTagsInSpan(SnapshotSpan span, HashSet<string> validKeys)
    {
        ITextSnapshot snapshot = span.Snapshot;
        int startLine = snapshot.GetLineNumberFromPosition(span.Start.Position);
        int endLine = snapshot.GetLineNumberFromPosition(span.End.Position);

        for (int lineIndex = startLine; lineIndex <= endLine; lineIndex++) {
            ITextSnapshotLine line = snapshot.GetLineFromLineNumber(lineIndex);
            string lineText = line.GetText();

            foreach (Match match in ArbXamlContext.ARB_KEY_REGEX.Matches(lineText)) {
                string key = match.Groups[1].Value;
                if (validKeys.Contains(key)) {
                    continue;
                }

                int keyStart = line.Start.Position + match.Groups[1].Index;
                SnapshotSpan keySpan = new(snapshot, new Span(keyStart, match.Groups[1].Length));
                if (!span.IntersectsWith(keySpan)) {
                    continue;
                }

                yield return new TagSpan<ErrorTag>(
                    keySpan,
                    new ErrorTag(
                        PredefinedErrorTypeNames.SyntaxError,
                        $"Unknown ARB key '{key}'"));
            }
        }
    }

    private void OnBufferChanged(object sender, TextContentChangedEventArgs e)
    {
        SnapshotSpan changedSpan = new(e.After, 0, e.After.Length);
        TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(changedSpan));
    }
}