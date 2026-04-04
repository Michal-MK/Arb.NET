using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Win32;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Interop;

namespace Arb.NET.IDE.VisualStudio.Tool.UI;

internal static class DialogUtilities {
    public static bool? ShowModal(DialogWindow dialog, DependencyObject? source = null) {
        ThreadHelper.ThrowIfNotOnUIThread();

        AttachOwner(dialog, source);
        dialog.ShowActivated = true;
        dialog.Loaded += OnDialogLoaded;
        return dialog.ShowDialog();

        void OnDialogLoaded(object sender, RoutedEventArgs e) {
            dialog.Loaded -= OnDialogLoaded;
            dialog.Activate();
            dialog.Focus();
        }
    }

    public static bool? ShowFileDialog(CommonDialog dialog, DependencyObject? source = null) {
        ThreadHelper.ThrowIfNotOnUIThread();

        Window? owner = ResolveOwnerWindow(source);
        return owner != null ? dialog.ShowDialog(owner) : dialog.ShowDialog();
    }

    public static MessageBoxResult ShowMessageBox(
        DependencyObject? source,
        string messageBoxText,
        string caption,
        MessageBoxButton button,
        MessageBoxImage icon) {
        ThreadHelper.ThrowIfNotOnUIThread();

        Window? owner = ResolveOwnerWindow(source);
        return owner != null
            ? MessageBox.Show(owner, messageBoxText, caption, button, icon)
            : MessageBox.Show(messageBoxText, caption, button, icon);
    }

    private static void AttachOwner(Window dialog, DependencyObject? source) {
        ThreadHelper.ThrowIfNotOnUIThread();

        Window? owner = ResolveOwnerWindow(source);
        if (owner != null && !ReferenceEquals(owner, dialog)) {
            dialog.Owner = owner;
            return;
        }

        if (ArbPackage.Instance == null) return;

        if (ServiceProvider.GlobalProvider.GetService(typeof(SVsUIShell)) is IVsUIShell uiShell) {
            uiShell.GetDialogOwnerHwnd(out IntPtr ownerHwnd);
            if (ownerHwnd != IntPtr.Zero) {
                new WindowInteropHelper(dialog).Owner = ownerHwnd;
            }
        }
    }

    private static Window? ResolveOwnerWindow(DependencyObject? source) {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (source is Window window) {
            return window;
        }

        if (source != null) {
            Window? owner = Window.GetWindow(source);
            if (owner != null) {
                return owner;
            }
        }

        return Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
    }
}