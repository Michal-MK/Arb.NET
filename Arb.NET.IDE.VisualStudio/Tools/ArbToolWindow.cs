using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;


namespace Arb.NET.IDE.VisualStudio.Tools {
    [Guid("b7e3c1d1-9c1b-4f0e-9f7c-8c7b8c8e2b11")]
    public class ArbToolWindow : ToolWindowPane {
        public ArbToolWindow() : base(null) {
            this.Caption = "Arb.NET Tool Window";

            // Optional: assign a WPF control
            this.Content = new System.Windows.Controls.TextBlock {
                Text = "This is an empty tool window.",
                Margin = new System.Windows.Thickness(10)
            };
        }
    }
}
