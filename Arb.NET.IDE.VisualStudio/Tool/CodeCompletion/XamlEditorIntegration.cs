using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text.RegularExpressions;
using Arb.NET.IDE.Common.Models;
using Arb.NET.IDE.Common.Services;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;

namespace Arb.NET.IDE.VisualStudio.Tool.CodeCompletion;

[Export(typeof(IVsTextViewCreationListener))]
[ContentType("xaml")]
[TextViewRole(PredefinedTextViewRoles.Editable)]
[Name("ArbXamlTextViewCreationListener")]
internal sealed class ArbXamlTextViewCreationListener : IVsTextViewCreationListener
{
    [Import]
    internal IVsEditorAdaptersFactoryService EditorAdaptersFactoryService { get; set; } = null!;

    [Import]
    internal ICompletionBroker CompletionBroker { get; set; } = null!;

    public void VsTextViewCreated(IVsTextView textViewAdapter)
    {
        IWpfTextView? textView = EditorAdaptersFactoryService.GetWpfTextView(textViewAdapter);
        if (textView == null) return;

        ArbXamlCommandFilter commandFilter = new(textView, CompletionBroker);
        textViewAdapter.AddCommandFilter(commandFilter, out IOleCommandTarget next);
        commandFilter.Next = next;
    }
}

internal sealed class ArbXamlCommandFilter(IWpfTextView textView, ICompletionBroker completionBroker) : IOleCommandTarget {

    public IOleCommandTarget? Next { get; set; }

    public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
    {
        return Next?.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText) ?? VSConstants.S_OK;
    }

    public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (pguidCmdGroup == VSConstants.VSStd2K &&
            nCmdID is (uint)VSConstants.VSStd2KCmdID.RETURN or (uint)VSConstants.VSStd2KCmdID.TAB) {
            if (TryCommitCompletion()) {
                return VSConstants.S_OK;
            }
        }

        bool isGotoDefinitionCommand =
            (pguidCmdGroup == VSConstants.GUID_VSStandardCommandSet97 && nCmdID == (uint)VSConstants.VSStd97CmdID.GotoDefn) ||
            (pguidCmdGroup == VSConstants.VSStd2K && nCmdID == (uint)VSConstants.VSStd2KCmdID.GOTOTYPEDEF);

        if (isGotoDefinitionCommand) {
            if (TryNavigateToArbEditor()) {
                return VSConstants.S_OK;
            }
        }

        return Next?.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut) ?? VSConstants.S_OK;
    }

    private bool TryCommitCompletion()
    {
        IReadOnlyList<ICompletionSession> sessions = completionBroker.GetSessions(textView);
        ICompletionSession? session = sessions.FirstOrDefault(s => !s.IsDismissed);
        if (session?.SelectedCompletionSet == null) return false;
        if (!session.SelectedCompletionSet.SelectionStatus.IsSelected) return false;

        session.Commit();
        return true;
    }

    private bool TryNavigateToArbEditor()
    {
        SnapshotPoint caretPoint = textView.Caret.Position.BufferPosition;
        ITextBuffer documentBuffer = textView.TextDataModel.DocumentBuffer;
        SnapshotPoint? documentPoint = textView.Caret.Position.Point.GetPoint(documentBuffer, textView.Caret.Position.Affinity);

        if (!ArbXamlContext.TryGetArbKeyAtPosition(caretPoint, out string key) &&
            !(documentPoint.HasValue && ArbXamlContext.TryGetArbKeyAtPosition(documentPoint.Value, out key))) return false;

        if (!textView.TextBuffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document) &&
            !documentBuffer.Properties.TryGetProperty(typeof(ITextDocument), out document)) return false;

        string? projectDir = ArbKeyService.FindProjectDirFromFilePath(document.FilePath);
        if (string.IsNullOrWhiteSpace(projectDir)) return false;

        ArbKeyInfo? keyInfo = ArbKeyService.GetKeys(projectDir!)
            .FirstOrDefault(it => string.Equals(it.Key, key, StringComparison.Ordinal));
        if (keyInfo?.ArbFilePath == null) return false;

        string rawKey = !string.IsNullOrWhiteSpace(keyInfo.RawKey)
            ? keyInfo.RawKey!
            : char.ToLowerInvariant(keyInfo.Key[0]) + keyInfo.Key.Substring(1);

        ArbNavigation.OpenToolWindowAtKey(keyInfo.ArbFilePath, rawKey);
        return true;
    }
}

internal static class ArbXamlContext
{
    // TODO(naive) Fails with global implicit xaml namespaces and probably more...
    internal static readonly Regex ARB_KEY_REGEX =
        new(@"\{[^:}]+:Arb\s+(\w+)\}?", RegexOptions.Compiled);

    public static bool TryGetArbKeyAtPosition(SnapshotPoint point, out string key)
    {
        ITextSnapshotLine line = point.GetContainingLine();
        int lineStart = line.Start.Position;
        string lineText = line.GetText();
        int column = point.Position - lineStart;

        foreach (Match match in ARB_KEY_REGEX.Matches(lineText)) {
            Group group = match.Groups[1];
            int keyStartColumn = group.Index;
            int keyEndColumnExclusive = group.Index + group.Length;

            if (column < keyStartColumn || column > keyEndColumnExclusive) continue;

            key = group.Value;
            return true;
        }

        key = string.Empty;
        return false;
    }
}

internal static class ArbNavigation
{
    public static void OpenToolWindowAtKey(string arbFilePath, string rawKey)
    {
        _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () => {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            ArbPackage? package = await ArbPackage.GetOrLoadAsync();
            if (package == null) {
                return;
            }

            ToolWindowPane window = await package.ShowToolWindowAsync(
                typeof(ArbToolWindow),
                0,
                true,
                package.DisposalToken);

            if (window is not ArbToolWindow arbWindow) {
                return;
            }

            await arbWindow.SetupIfNeededAsync(package.ColumnSettingsService, package.ArbService, package.TranslationSettingsService);
            await arbWindow.NavigateToArbKeyAsync(arbFilePath, rawKey);
        });
    }
}