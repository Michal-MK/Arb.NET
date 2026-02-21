using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.Runtime.InteropServices;
using Arb.NET.IDE.VisualStudio.Tool.Services;
using Arb.NET.IDE.VisualStudio.Tool.Services.Persistence;
using Arb.NET.IDE.VisualStudio.Tool.UI;

namespace Arb.NET.IDE.VisualStudio.Tool;

[Guid("b7e3c1d1-9c1b-4f0e-9f7c-8c7b8c8e2b11")]
public sealed class ArbToolWindow : ToolWindowPane, IVsSelectionEvents, IVsSolutionEvents {
    private readonly ArbEditorControl control;

    public ArbToolWindow() : base(null) {
        Caption = "Arb.NET";
        control = new ArbEditorControl();
        Content = control;
    }
    
    [SuppressMessage("ReSharper", "ParameterHidesMember")]
    public void Setup(ColumnSettingsService columnSettingsService, ArbService arbService, TranslationSettingsService translationSettingsService) {
        control.Initialize((ArbPackage)Package, columnSettingsService, arbService, translationSettingsService);
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