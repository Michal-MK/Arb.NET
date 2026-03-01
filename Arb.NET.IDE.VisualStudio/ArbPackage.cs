using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using Arb.NET.IDE.VisualStudio.Tool;
using Arb.NET.IDE.VisualStudio.Tool.Services;
using Arb.NET.IDE.VisualStudio.Tool.Services.Persistence;

namespace Arb.NET.IDE.VisualStudio;

/// <summary>
/// This is the class that implements the package exposed by this assembly.
/// </summary>
/// <remarks>
/// <para>
/// The minimum requirement for a class to be considered a valid package for Visual Studio
/// is to implement the IVsPackage interface and register itself with the shell.
/// This package uses the helper classes defined inside the Managed Package Framework (MPF)
/// to do it: it derives from the Package class that provides the implementation of the
/// IVsPackage interface and uses the registration attributes defined in the framework to
/// register itself and its components with the shell. These attributes tell the pkgdef creation
/// utility what data to put into .pkgdef file.
/// </para>
/// <para>
/// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
/// </para>
/// </remarks>
[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[ProvideMenuResource("Menus.ctmenu", 1)]
[Guid(PACKAGE_GUID_STRING)]
[ProvideToolWindow(typeof(ArbToolWindow))]
public sealed class ArbPackage : AsyncPackage {
    internal static ArbPackage? Instance { get; private set; }

    private const string PACKAGE_GUID_STRING = "3191a384-5cf6-4e40-bd0f-3a6dcd5dc05f";

    internal ColumnSettingsService? ColumnSettingsService { get; private set; }
    internal ArbService? ArbService { get; private set; }
    internal TranslationSettingsService? TranslationSettingsService { get; private set; }

    internal static async Task<ArbPackage?> GetOrLoadAsync() {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        if (Instance != null) return Instance;

        if (ServiceProvider.GlobalProvider.GetService(typeof(SVsShell)) is IVsShell shell) {
            Guid packageGuid = new(PACKAGE_GUID_STRING);
            shell.LoadPackage(ref packageGuid, out IVsPackage _);
        }

        return Instance;
    }

    /// <summary>
    /// Initialization of the package; this method is called right after the package is sited, so this is the place
    /// where you can put all the initialization code that rely on services provided by VisualStudio.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
    /// <param name="progress">A provider for progress updates.</param>
    /// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
    protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress) {
        // When initialized asynchronously, the current thread may be a background thread at this point.
        // Do any initialization that requires the UI thread after switching to the UI thread.
        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        Instance = this;

        ColumnSettingsService = new ColumnSettingsService(this);
        ArbService = new ArbService(this);
        TranslationSettingsService = new TranslationSettingsService(this);

        await ShowArbToolWindowCommand.InitializeAsync(this, ColumnSettingsService, ArbService, TranslationSettingsService);
        await OpenArbEditorFromContextMenuCommand.InitializeAsync(this);

        // If VS restored the tool window before InitializeAsync ran (window was open in the
        // previous session), OnToolWindowCreated will have fired but found null services.
        // Now that services are ready, find the window and set it up if still uninitialized.
        if (FindToolWindow(typeof(ArbToolWindow), 0, false) is ArbToolWindow restoredWindow) {
            await restoredWindow.SetupIfNeededAsync(ColumnSettingsService, ArbService, TranslationSettingsService);
        }
    }
}