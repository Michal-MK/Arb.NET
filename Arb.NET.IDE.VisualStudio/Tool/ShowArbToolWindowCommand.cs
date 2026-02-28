using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Design;
using System.Threading.Tasks;
using Arb.NET.IDE.VisualStudio.Tool.Services;
using Arb.NET.IDE.VisualStudio.Tool.Services.Persistence;

namespace Arb.NET.IDE.VisualStudio.Tool;

internal sealed class ShowArbToolWindowCommand {
    private const int COMMAND_ID = 0x0100;

    // This GUID must match the one in the .vsct file for the command's parent menu/toolbar
    private static readonly Guid COMMAND_SET = new("d8c8b3e1-2f3c-4b6c-9d4e-1f2b3c4d5e6f");

    private readonly ArbPackage package;
    private readonly ColumnSettingsService columnSettingsService;
    private readonly ArbService arbService;
    private readonly TranslationSettingsService translationSettingsService;

    private ShowArbToolWindowCommand(ArbPackage package, OleMenuCommandService commandService, ColumnSettingsService columnSettingsService, ArbService arbService, TranslationSettingsService translationSettingsService) {
        this.package = package;
        this.columnSettingsService = columnSettingsService;
        this.arbService = arbService;
        this.translationSettingsService = translationSettingsService;

        CommandID cmdId = new(COMMAND_SET, COMMAND_ID);
        OleMenuCommand cmd = new(Execute, cmdId);

        commandService.AddCommand(cmd);
    }

    public static async Task InitializeAsync(ArbPackage package, ColumnSettingsService columnSettingsService, ArbService arbService, TranslationSettingsService translationSettingsService) {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        if (await package.GetServiceAsync(typeof(IMenuCommandService)) is not OleMenuCommandService commandService) {
            throw new InvalidOperationException("Unable to get OleMenuCommandService.");
        }

        // ReSharper disable once ObjectCreationAsStatement
        new ShowArbToolWindowCommand(package, commandService, columnSettingsService, arbService, translationSettingsService);
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

            if (window is ArbToolWindow arbWindow) {
                // OnToolWindowCreated already runs on both user-open and restore, but if services
                // were not yet ready at that point (restore case handled by InitializeAsync),
                // SetupIfNeededAsync is idempotent and safe to call again here.
                await arbWindow.SetupIfNeededAsync(columnSettingsService, arbService, translationSettingsService);
            }

            // TODO(handle) unsubscription
        });
    }
}