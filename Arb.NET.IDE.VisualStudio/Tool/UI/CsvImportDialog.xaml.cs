using Arb.NET.IDE.Common.Models;
using Microsoft.VisualStudio.PlatformUI;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Arb.NET.IDE.VisualStudio.Tool.UI;

public partial class CsvImportDialog : DialogWindow {
    private const string IgnoreMapping = "";
    private const string KeyMapping = "key";

    private readonly List<ComboBox> mappingBoxes = [];
    private readonly List<CsvImportMappingOption> mappingOptions = [];

    public List<string> SelectedMappings { get; } = [];

    public CsvImportMode SelectedImportMode => ReplaceModeRadio.IsChecked == true
        ? CsvImportMode.ReplaceAll
        : CsvImportMode.Merge;

    public CsvImportDialog(CsvImportPreview preview, string sourceName) {
        InitializeComponent();

        Title = "Import CSV";
        Width = 1080;
        Height = 720;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        HasMaximizeButton = true;
        HasMinimizeButton = false;
        ShowActivated = true;

        Loaded += (_, _) => {
            Topmost = true;
            Activate();
            Focus();
            Topmost = false;
        };

        IntroText.Text = $"Review the header mapping for '{sourceName}'. The CSV header is shown in the first row, the editable mapping row is below it, and the table shows the remaining values.";

        mappingOptions.Add(new CsvImportMappingOption(IgnoreMapping, "Ignore"));
        mappingOptions.Add(new CsvImportMappingOption(KeyMapping, "Key"));
        mappingOptions.AddRange(preview.AvailableLocaleMappings.Select(locale => new CsvImportMappingOption(locale, locale)));

        BuildMappingGrid(preview);
        BuildPreviewGrid(preview);
    }

    private void BuildMappingGrid(CsvImportPreview preview) {
        MappingGrid.RowDefinitions.Clear();
        MappingGrid.ColumnDefinitions.Clear();
        MappingGrid.Children.Clear();
        mappingBoxes.Clear();

        MappingGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        MappingGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        for (int index = 0; index < preview.Headers.Count; index++) {
            MappingGrid.ColumnDefinitions.Add(new ColumnDefinition {
                Width = new GridLength(180)
            });

            Border headerBorder = CreateCellBorder();
            TextBlock headerText = new() {
                Text = string.IsNullOrWhiteSpace(preview.Headers[index]) ? "(empty)" : preview.Headers[index],
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(6, 4, 6, 4),
                Foreground = GetThemeBrush(EnvironmentColors.ToolWindowTextBrushKey)
            };
            headerBorder.Child = headerText;
            Grid.SetColumn(headerBorder, index);
            Grid.SetRow(headerBorder, 0);
            MappingGrid.Children.Add(headerBorder);

            Border mappingBorder = CreateCellBorder();
            ComboBox mappingBox = new() {
                Margin = new Thickness(4),
                MinWidth = 150,
                DisplayMemberPath = nameof(CsvImportMappingOption.Label),
                ItemsSource = mappingOptions
            };

            string suggested = preview.SuggestedMappings.ElementAtOrDefault(index) ?? IgnoreMapping;
            mappingBox.SelectedItem = mappingOptions.FirstOrDefault(option => string.Equals(option.Value, suggested, System.StringComparison.OrdinalIgnoreCase))
                                      ?? mappingOptions[0];

            mappingBoxes.Add(mappingBox);
            mappingBorder.Child = mappingBox;
            Grid.SetColumn(mappingBorder, index);
            Grid.SetRow(mappingBorder, 1);
            MappingGrid.Children.Add(mappingBorder);
        }
    }

    private void BuildPreviewGrid(CsvImportPreview preview) {
        PreviewGrid.Columns.Clear();
        for (int index = 0; index < preview.Headers.Count; index++) {
            PreviewGrid.Columns.Add(new DataGridTextColumn {
                Header = string.IsNullOrWhiteSpace(preview.Headers[index]) ? "(empty)" : preview.Headers[index],
                Binding = new Binding($"Cells[{index}]"),
                Width = new DataGridLength(180)
            });
        }

        PreviewGrid.ItemsSource = preview.Rows.Select(row => new CsvImportValueRow(row.Cells)).ToList();
    }

    private Border CreateCellBorder() {
        return new Border {
            BorderThickness = new Thickness(0, 0, 1, 1),
            BorderBrush = GetThemeBrush(EnvironmentColors.ToolWindowBorderBrushKey),
            Background = GetThemeBrush(EnvironmentColors.ToolWindowBackgroundBrushKey)
        };
    }

    private Brush GetThemeBrush(object resourceKey) {
        return (Brush)(TryFindResource(resourceKey) ?? Brushes.Transparent);
    }

    private void ImportButton_OnClick(object sender, RoutedEventArgs e) {
        List<string> mappings = mappingBoxes
            .Select(box => (box.SelectedItem as CsvImportMappingOption)?.Value ?? IgnoreMapping)
            .ToList();

        if (mappings.Count(mapping => string.Equals(mapping, KeyMapping, System.StringComparison.OrdinalIgnoreCase)) != 1) {
            MessageBox.Show("Select exactly one column mapped to Key.", "Arb.NET", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        List<string> duplicateLocales = mappings
            .Where(mapping => !string.IsNullOrWhiteSpace(mapping) && !string.Equals(mapping, KeyMapping, System.StringComparison.OrdinalIgnoreCase))
            .GroupBy(mapping => mapping, System.StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicateLocales.Count > 0) {
            MessageBox.Show($"Each locale can be mapped only once. Duplicate mappings: {string.Join(", ", duplicateLocales)}.", "Arb.NET", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SelectedMappings.Clear();
        SelectedMappings.AddRange(mappings);
        DialogResult = true;
    }
}

internal sealed class CsvImportValueRow(IEnumerable<string> cells) {
    public List<string> Cells { get; } = [..cells];
}

internal sealed class CsvImportMappingOption(string value, string label) {
    public string Value { get; } = value;
    public string Label { get; } = label;
}