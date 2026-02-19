using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Design;
using System.Threading.Tasks;

namespace Arb.NET.IDE.VisualStudio.Tools {
    internal sealed class ShowArbToolWindowCommand {
        public const int CommandId = 0x0100;
        public static readonly Guid CommandSet = new Guid("d8c8b3e1-2f3c-4b6c-9d4e-1f2b3c4d5e6f");

        private readonly AsyncPackage package;

        private ShowArbToolWindowCommand(AsyncPackage package, Microsoft.VisualStudio.Shell.OleMenuCommandService commandService) {
            this.package = package;

            var cmdId = new CommandID(CommandSet, CommandId);
            var cmd = new OleMenuCommand(Execute, cmdId);

            commandService.AddCommand(cmd);
        }

        public static async Task InitializeAsync(AsyncPackage package) {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService))
                                   as Microsoft.VisualStudio.Shell.OleMenuCommandService;

            if (commandService == null) {
                throw new InvalidOperationException("Unable to get OleMenuCommandService.");
            }

            new ShowArbToolWindowCommand(package, commandService);
        }

        private void Execute(object sender, EventArgs e) {
            ThreadHelper.ThrowIfNotOnUIThread();

            package.JoinableTaskFactory.RunAsync(async () => {
                var window = await package.ShowToolWindowAsync(
                    typeof(ArbToolWindow),
                    0,
                    true,
                    package.DisposalToken);
            });
        }
    }
}
