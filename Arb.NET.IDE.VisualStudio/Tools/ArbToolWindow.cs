using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.Runtime.InteropServices;

namespace Arb.NET.IDE.VisualStudio.Tools;

[Guid("b7e3c1d1-9c1b-4f0e-9f7c-8c7b8c8e2b11")]
public sealed class ArbToolWindow : ToolWindowPane, IVsSelectionEvents {
    private readonly ArbEditorControl control;

    public ArbToolWindow() : base(null) {
        Caption = "Arb.NET";
        control = new ArbEditorControl();
        Content = control;
    }

    public override void OnToolWindowCreated() {
        base.OnToolWindowCreated();
        control.Initialize((AsyncPackage)Package);
    }

    // IVsSelectionEvents - registered via IVsMonitorSelection.AdviseSelectionEvents.
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
}