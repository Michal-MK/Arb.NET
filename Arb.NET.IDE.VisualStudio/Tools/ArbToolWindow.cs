using Microsoft.VisualStudio.Shell;
using System.Runtime.InteropServices;

namespace Arb.NET.IDE.VisualStudio.Tools;

[Guid("b7e3c1d1-9c1b-4f0e-9f7c-8c7b8c8e2b11")]
public sealed class ArbToolWindow : ToolWindowPane {
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
}