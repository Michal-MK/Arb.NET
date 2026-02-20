using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Design;
using System.Threading.Tasks;

namespace Arb.NET.IDE.VisualStudio.Tools;

internal sealed class ShowArbToolWindowCommand {
    private const int COMMAND_ID = 0x0100;
    // This GUID must match the one in the .vsct file for the command's parent menu/toolbar
    private static readonly Guid COMMAND_SET = new("d8c8b3e1-2f3c-4b6c-9d4e-1f2b3c4d5e6f");

    private readonly AsyncPackage package;
    private uint selectionCookie;

    private ShowArbToolWindowCommand(AsyncPackage package, OleMenuCommandService commandService) {
        this.package = package;

        CommandID cmdId = new(COMMAND_SET, COMMAND_ID);
        OleMenuCommand cmd = new(Execute, cmdId);

        commandService.AddCommand(cmd);
    }

    public static async Task InitializeAsync(AsyncPackage package) {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        if (await package.GetServiceAsync(typeof(IMenuCommandService)) is not OleMenuCommandService commandService) {
            throw new InvalidOperationException("Unable to get OleMenuCommandService.");
        }

        // ReSharper disable once ObjectCreationAsStatement
        new ShowArbToolWindowCommand(package, commandService);
    }

    private void Execute(object sender, EventArgs e) {
        _ = package.JoinableTaskFactory.RunAsync(async () => {
            ToolWindowPane window = await package.ShowToolWindowAsync(
                typeof(ArbToolWindow),
                0,
                true,
                package.DisposalToken);

            if (window == null) {
                throw new InvalidOperationException("Failed to show Arb.NET tool window.");
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // Subscribe to global selection events once so we can detect when our tool
            // window frame becomes the active window frame (Focus handling).
            if (selectionCookie == 0 && window is ArbToolWindow arbWindow) {
                if (await package.GetServiceAsync(typeof(SVsShellMonitorSelection)) is IVsMonitorSelection monitorSelection) {
                    monitorSelection.AdviseSelectionEvents(arbWindow, out selectionCookie);
                }
            }
        });
    }
}