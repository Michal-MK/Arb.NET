using Microsoft.VisualStudio.PlatformUI;
using System.Windows;

namespace Arb.NET.IDE.VisualStudio.Tools;

public partial class ArbInputDialog : DialogWindow {

    public string Result { get; private set; }

    public ArbInputDialog(string title, string labelText, string defaultText) {
        InitializeComponent();
        Title = title;
        Width = 400;
        Height = 140;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        HasMaximizeButton = false;
        HasMinimizeButton = false;

        PromptLabel.Text = labelText;
        InputBox.Text = defaultText;

        InputBox.Loaded += (_, _) => {
            InputBox.SelectAll();
            InputBox.Focus();
        };
    }

    private void OkButton_OnClick(object sender, RoutedEventArgs e) {
        Result = InputBox.Text;
        DialogResult = true;
    }
}
