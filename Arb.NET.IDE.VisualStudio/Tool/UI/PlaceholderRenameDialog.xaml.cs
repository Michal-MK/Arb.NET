using Microsoft.VisualStudio.PlatformUI;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Arb.NET.IDE.VisualStudio.Tool.Models;
using System;

namespace Arb.NET.IDE.VisualStudio.Tool.UI;

public partial class PlaceholderRenameDialog : DialogWindow {
    private readonly ObservableCollection<PlaceholderRenameItem> items;

    public IReadOnlyList<PlaceholderRenameItem> ResultItems => items.ToList();

    public PlaceholderRenameDialog(string keyName, IEnumerable<string> placeholders) {
        InitializeComponent();

        List<string> placeholderList = placeholders.ToList();

        Title = "Rename Placeholders";
        Width = 760;
        Height = Math.Min(340, 150 + (placeholderList.Count * 38));
        MinWidth = 560;
        MinHeight = 220;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        HasMaximizeButton = true;
        HasMinimizeButton = false;

        PromptLabel.Text = $"Update placeholder names for key '{keyName}'. Edit only the rows you want to rename.";

        items = new ObservableCollection<PlaceholderRenameItem>(
            placeholderList.Select(name => new PlaceholderRenameItem(name)));
        RowsItemsControl.ItemsSource = items;
    }

    private void OkButton_OnClick(object sender, RoutedEventArgs e) {
        DialogResult = true;
    }
}