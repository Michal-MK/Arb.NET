using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Design;
using System.IO;
using System.Threading.Tasks;
using Arb.NET.IDE.Common.Services;
using EnvDTE;

namespace Arb.NET.IDE.VisualStudio.Tool;

/// <summary>
/// Registers the "Open Arb.NET Editor" command that appears in the Solution Explorer
/// context menu when right-clicking a folder that contains .arb files, an .arb file,
/// or a l10n.yaml file.
/// </summary>
internal sealed class OpenArbEditorFromContextMenuCommand {
    // Must match guidArbContextCmdSet / OpenArbEditorFromFolderCommand in ArbPackage.vsct
    private static readonly Guid COMMAND_SET = new("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
    private const int COMMAND_ID = 0x0101;

    private readonly ArbPackage package;

    private OpenArbEditorFromContextMenuCommand(
        ArbPackage package,
        OleMenuCommandService commandService
    ) {
        this.package = package;

        CommandID cmdId = new(COMMAND_SET, COMMAND_ID);
        OleMenuCommand cmd = new(Execute, cmdId);
        cmd.BeforeQueryStatus += OnBeforeQueryStatus;
        commandService.AddCommand(cmd);
    }

    public static async Task InitializeAsync(
        ArbPackage package
    ) {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        if (await package.GetServiceAsync(typeof(IMenuCommandService)) is not OleMenuCommandService commandService) {
            throw new InvalidOperationException("Unable to get OleMenuCommandService.");
        }

        // ReSharper disable once ObjectCreationAsStatement
        new OpenArbEditorFromContextMenuCommand(package, commandService);
    }

    private void OnBeforeQueryStatus(object sender, EventArgs e) {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (sender is not OleMenuCommand cmd) return;
        cmd.Visible = false;

        string? selectedPath = GetSelectedItemPath();
        if (selectedPath == null) return;

        cmd.Visible = IsRelevantPath(selectedPath);
    }

    private void Execute(object sender, EventArgs e) {
        _ = package.JoinableTaskFactory.RunAsync(async () => {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            string? selectedPath = GetSelectedItemPath();
            if (selectedPath == null) return;

            string? arbDirectory = ResolveArbDirectory(selectedPath);
            if (arbDirectory == null) return;

            ToolWindowPane window = await package.ShowToolWindowAsync(
                typeof(ArbToolWindow), 0, true, package.DisposalToken
            );

            if (window is not ArbToolWindow arbWindow) return;

            await arbWindow.NavigateToDirectoryAsync(arbDirectory);
        });
    }

    private string? GetSelectedItemPath() {
        ThreadHelper.ThrowIfNotOnUIThread();

        DTE? dte = ((IServiceProvider)package).GetService(typeof(DTE)) as DTE;
        if (dte?.SelectedItems == null || dte.SelectedItems.Count == 0) return null;

        SelectedItem item = dte.SelectedItems.Item(1);

        // Could be a project item (file) or a project/folder
        if (item.ProjectItem != null) {
            try {
                return item.ProjectItem.Properties.Item("FullPath").Value as string;
            }
            catch {
                return null;
            }
        }

        if (item.Project == null) return null;
        
        try {
            string? dir = Path.GetDirectoryName(item.Project.FullName);
            return dir;
        }
        catch {
            return null;
        }
    }

    private static bool IsRelevantPath(string path) {
        if (Directory.Exists(path)) {
            // Show for folders that directly contain .arb files
            return Directory.GetFiles(path, "*.arb", SearchOption.TopDirectoryOnly).Length > 0;
        }

        string fileName = Path.GetFileName(path);
        if (string.Equals(fileName, "l10n.yaml", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(Path.GetExtension(path), ".arb", StringComparison.OrdinalIgnoreCase)) return true;

        return false;
    }

    /// <param name="path">ARB holding directory or individual ARB file or l10n.yaml file</param>
    private static string? ResolveArbDirectory(string path) {
        // if ARB directory
        if (Directory.Exists(path)) {
            return Directory.GetFiles(path, "*.arb", SearchOption.TopDirectoryOnly).Length > 0
                ? path
                : null;
        }

        string? dir = Path.GetDirectoryName(path);
        if (dir == null) return null;

        string fileName = Path.GetFileName(path);
        if (string.Equals(fileName, "l10n.yaml", StringComparison.OrdinalIgnoreCase)) {
            return LocalizationYamlService.ResolveArbDirectory(dir);
        }

        if (string.Equals(Path.GetExtension(path), ".arb", StringComparison.OrdinalIgnoreCase)) {
            return dir;
        }

        return null;
    }
}