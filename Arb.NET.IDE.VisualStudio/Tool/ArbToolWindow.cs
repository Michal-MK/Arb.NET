using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Arb.NET.IDE.VisualStudio.Tool.Services;
using Arb.NET.IDE.VisualStudio.Tool.Services.Persistence;
using Arb.NET.IDE.VisualStudio.Tool.UI;

namespace Arb.NET.IDE.VisualStudio.Tool;

[Guid("b7e3c1d1-9c1b-4f0e-9f7c-8c7b8c8e2b11")]
public sealed class ArbToolWindow : ToolWindowPane, IVsSelectionEvents, IVsSolutionEvents {
    private ArbPackage package;
    private readonly ArbEditorControl control;
    private bool initialized;
    private uint selectionCookie;
    private uint solutionEventsCookie;

    public ArbToolWindow() : base(null) {
        Caption = "Arb.NET";
        control = new ArbEditorControl();
        Content = control;
    }

    /// <summary>
    /// Called by VS after the window has been fully sited — on both user-open and session restore.
    /// Resolves services from the package and initializes the control.
    /// This is somehow called before the constructor above!
    /// </summary>
    public override void OnToolWindowCreated() {
        base.OnToolWindowCreated();
        package = (ArbPackage)Package;
        _ = SetupFromPackageAsync();
    }

    private async Task SetupFromPackageAsync() {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        await SetupIfNeededAsync(package.ColumnSettingsService, package.ArbService, package.TranslationSettingsService);
    }

    [SuppressMessage("ReSharper", "ParameterHidesMember")]
    public async Task SetupIfNeededAsync(
        ColumnSettingsService? columnSettingsService,
        ArbService? arbService,
        TranslationSettingsService? translationSettingsService
    ) {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        if (initialized) return;
        initialized = true;

        if (columnSettingsService is null || arbService is null || translationSettingsService is null) return;

        control.Initialize(package, columnSettingsService, arbService, translationSettingsService);

        // Subscribe to global selection events so we can detect when our tool window frame
        // becomes the active window frame (triggers RefreshData).
        if (selectionCookie == 0 && Package != null) {
            if (await package.GetServiceAsync(typeof(SVsShellMonitorSelection)) is IVsMonitorSelection monitorSelection) {
                monitorSelection.AdviseSelectionEvents(this, out selectionCookie);
            }
        }

        // Subscribe to solution events so data reloads when a solution opens/closes.
        if (solutionEventsCookie == 0 && Package != null) {
            if (await package.GetServiceAsync(typeof(SVsSolution)) is IVsSolution solution) {
                solution.AdviseSolutionEvents(this, out solutionEventsCookie);
            }
        }
    }

    #region IVsSelectionEvents - registered via IVsMonitorSelection.AdviseSelectionEvents in ShowArbToolWindowCommand

    public int OnElementValueChanged(uint elementid, object varValueOld, object varValueNew) {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (elementid == (uint)VSConstants.VSSELELEMID.SEID_WindowFrame &&
            varValueNew is IVsWindowFrame newFrame &&
            Frame is IVsWindowFrame ownFrame &&
            newFrame == ownFrame) {
            control.RefreshData();
        }
        return VSConstants.S_OK;
    }

    public int OnSelectionChanged(
        IVsHierarchy pHierOld,
        uint itemidOld,

        // ReSharper disable once InconsistentNaming
        IVsMultiItemSelect pMISOld,

        // ReSharper disable once InconsistentNaming
        ISelectionContainer pSCOld,
        IVsHierarchy pHierNew,
        uint itemidNew,

        // ReSharper disable once InconsistentNaming
        IVsMultiItemSelect pMISNew,

        // ReSharper disable once InconsistentNaming
        ISelectionContainer pSCNew
    ) => VSConstants.S_OK;

    public int OnCmdUIContextChanged(uint dwCmdUICookie, int fActive) => VSConstants.S_OK;

    #endregion

    #region IVsSolutionEvents - registered via IVsSolution.AdviseSolutionEvents in ShowArbToolWindowCommand

    public int OnAfterOpenSolution(object pUnkReserved, int fNewSolution) {
        _ = control.LoadDataAsync();
        return VSConstants.S_OK;
    }

    public int OnAfterCloseSolution(object pUnkReserved) {
        _ = control.LoadDataAsync();
        return VSConstants.S_OK;
    }

    public int OnBeforeCloseSolution(object pUnkReserved) => VSConstants.S_OK;
    public int OnQueryCloseSolution(object pUnkReserved, ref int pfCancel) => VSConstants.S_OK;
    public int OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded) => VSConstants.S_OK;
    public int OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel) => VSConstants.S_OK;
    public int OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved) => VSConstants.S_OK;
    public int OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy) => VSConstants.S_OK;
    public int OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel) => VSConstants.S_OK;
    public int OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy) => VSConstants.S_OK;

    #endregion
}