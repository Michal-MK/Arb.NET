using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Input;
using Arb.NET.IDE.VisualStudio.Tool.Models;
using Arb.NET.IDE.VisualStudio.Tool.Services;
using Arb.NET.IDE.VisualStudio.Tool.Services.Persistence;
using Arb.NET.IDE.VisualStudio.Tool.UI.Converters;

namespace Arb.NET.IDE.VisualStudio.Tool.UI;

public partial class ArbEditorControl : UserControl {
    private AsyncPackage? package;

    private ColumnSettingsService columnSettingsService = null!;
    private ArbService arbService = null!;
    private TranslationSettingsService translationSettingsService = null!;

    private const double DEFAULT_KEY_COLUMN_WIDTH = 240;
    private const double DEFAULT_LOCALE_COLUMN_WIDTH = 220;

    private ArbScanResult arbScanResult = null!;

    private List<string> currentLangCodes = [];
    private ObservableCollection<ArbRow> currentRows = [];
    private string? currentDirectory;
    private bool suppressSave;
    private readonly List<(DependencyPropertyDescriptor Dpd, DataGridColumn Col, EventHandler Handler)> widthListeners = [];

    private readonly ArbParser parser = new();

    public ArbEditorControl() {
        InitializeComponent();
        ArbGrid.ColumnDisplayIndexChanged += ArbGrid_OnColumnDisplayIndexChanged;
    }

    [SuppressMessage("ReSharper", "ParameterHidesMember")]
    public void Initialize(AsyncPackage package, ColumnSettingsService columnSettingsService, ArbService arbService, TranslationSettingsService translationSettingsService) {
        this.package = package;
        this.columnSettingsService = columnSettingsService;
        this.arbService = arbService;
        this.translationSettingsService = translationSettingsService;
        _ = LoadDataAsync();
    }

    internal async Task LoadDataAsync() {
        arbScanResult = await arbService.ScanArbFilesAsync();

        if (arbScanResult.SolutionNotLoaded) {
            LoadingLabel.Text = "No solution open. Open a solution and reopen this window.";
            return;
        }

        if (arbScanResult.ArbErrors.Count > 0) {
            MessageBox.Show($"Completed with {arbScanResult.ArbErrors.Count} error(s):\n" +
                            string.Join("\n", arbScanResult.ArbErrors.Select(ex => $"  {ex.Message}")),
                            "Arb.NET - Exception", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        if (arbScanResult.DirGroupedArbFiles.Count == 0) {
            LoadingLabel.Text = "No .arb files found in this solution.";
            return;
        }

        LoadingLabel.Visibility = Visibility.Collapsed;
        ArbGrid.Visibility = Visibility.Visible;

        UpdateRelativePathConverter();
        List<string> sortedDirs = arbScanResult.DirGroupedArbFiles.Keys.OrderBy(d => d).ToList();
        DirectoryCombo.ItemsSource = sortedDirs;
        DirectoryCombo.SelectedIndex = 0;
    }

    private void UpdateRelativePathConverter() {
        if (Resources["RelativePathConverter"] is RelativePathConverter converter) {
            converter.ScanResult = arbScanResult;
        }
    }

    public void RefreshData() {
        if (package == null) return;
        _ = RefreshDataAsync();
    }

    private async Task RefreshDataAsync() {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        arbScanResult = await arbService.ScanArbFilesAsync();
        UpdateRelativePathConverter();

        if (arbScanResult.SolutionNotLoaded) {
            LoadingLabel.Text = "No solution open. Open a solution and reopen this window.";
            return;
        }

        if (arbScanResult.ArbErrors.Count > 0) {
            MessageBox.Show($"Completed refresh with {arbScanResult.ArbErrors.Count} errors:\n" +
                            string.Join("\n", arbScanResult.ArbErrors.Select(ex => $"  {ex.Message}")),
                            "Arb.NET - Refresh Errors", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        if (arbScanResult.DirGroupedArbFiles.Count == 0) {
            LoadingLabel.Text = "No .arb files found in this solution.";
            return;
        }

        if (DirectoryCombo.SelectedItem is string currentDir && arbScanResult.DirGroupedArbFiles.ContainsKey(currentDir)) {
            BuildTable(currentDir);
        }
        else if (arbScanResult.DirGroupedArbFiles.Count > 0) {
            List<string> sortedDirs = arbScanResult.DirGroupedArbFiles.Keys.OrderBy(d => d).ToList();
            DirectoryCombo.ItemsSource = sortedDirs;
            DirectoryCombo.SelectedIndex = 0;
        }
    }

    private void BuildTable(string directory) {
        if (!arbScanResult.DirGroupedArbFiles.TryGetValue(directory, out List<ArbFile> arbFiles)) return;

        suppressSave = true;

        SortedSet<string> allKeys = new(StringComparer.Ordinal);
        Dictionary<string, Dictionary<string, string>> byLocale = new(StringComparer.Ordinal);

        foreach (ArbFile arb in arbFiles) {
            try {
                string content = File.ReadAllText(arb.FilePath);
                ArbParseResult result = parser.ParseContent(content);

                if (!result.ValidationResults.IsValid) {
                    MessageBox.Show($"Failed to build table due to ARB file validation errors in {arb.FilePath}:\n" +
                                    string.Join("\n", result.ValidationResults.Errors.Select(e => $"  [{e.Keyword}] {e.Message} at {e.InstanceLocation}")),
                                    "Arb.NET", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (result.Document == null) continue;

                Dictionary<string, string> values = new(StringComparer.Ordinal);
                foreach (KeyValuePair<string, ArbEntry> kvp in result.Document.Entries) {
                    allKeys.Add(kvp.Key);
                    values[kvp.Key] = kvp.Value.Value;
                }
                byLocale[arb.LangCode] = values;
            }
            catch {
                byLocale[arb.LangCode] = new Dictionary<string, string>();
            }
        }

        // Restore user-defined column order; unknown/new locales are appended alphabetically at the end.
        List<string>? savedOrder = columnSettingsService.LoadLocaleOrder(directory);
        List<string> alphabetical = arbFiles.Select(lf => lf.LangCode).OrderBy(l => l).ToList();
        if (savedOrder != null) {
            currentLangCodes = [
                .. savedOrder.Where(alphabetical.Contains),
                .. alphabetical.Where(l => !savedOrder.Contains(l))
            ];
        }
        else {
            currentLangCodes = alphabetical;
        }

        ObservableCollection<ArbRow> rows = [];
        foreach (string key in allKeys) {
            ArbRow row = new(key);
            foreach (string locale in currentLangCodes) {
                row.Values[locale] = byLocale.TryGetValue(locale, out var vals) && vals.TryGetValue(key, out string val)
                    ? val
                    : string.Empty;
            }
            rows.Add(row);
        }
        currentRows = rows;
        currentDirectory = directory;

        Dictionary<string, double> savedWidths = columnSettingsService.LoadColumnWidths(directory);

        // Detach previous width listeners before clearing columns.
        DependencyPropertyDescriptor widthDpd = DependencyPropertyDescriptor.FromProperty(DataGridColumn.WidthProperty, typeof(DataGridColumn));
        foreach ((DependencyPropertyDescriptor dpd, DataGridColumn col, EventHandler handler) in widthListeners) {
            dpd.RemoveValueChanged(col, handler);
        }
        widthListeners.Clear();

        ArbGrid.Columns.Clear();
        ArbGrid.Columns.Add(new DataGridTextColumn {
            Header = "Key",
            Binding = new Binding("Key"),
            IsReadOnly = true,
            Width = savedWidths.TryGetValue("Key", out double kw) ? kw : DEFAULT_KEY_COLUMN_WIDTH
        });
        foreach (string locale in currentLangCodes) {
            ArbGrid.Columns.Add(new DataGridTextColumn {
                Header = locale,
                Binding = new Binding($"Values[{locale}]"),
                Width = savedWidths.TryGetValue(locale, out double lw) ? lw : DEFAULT_LOCALE_COLUMN_WIDTH
            });
        }

        ArbGrid.ItemsSource = currentRows;
        suppressSave = false;

        // Attach width-change listeners after suppressSave is cleared so the initial layout fires are ignored.
        foreach (DataGridColumn col in ArbGrid.Columns) {

            widthDpd.AddValueChanged(col, Handler);
            widthListeners.Add((widthDpd, col, Handler));
            continue;

            void Handler(object _, EventArgs _1) {
                if (!suppressSave && currentDirectory != null) columnSettingsService.SaveColumnWidths(currentDirectory, ArbGrid.Columns);
            }
        }
    }

    private bool ModifyArbFile(ArbFile arb, Action<ArbDocument> mutate) {
        try {
            string content = File.ReadAllText(arb.FilePath);
            ArbParseResult parsed = parser.ParseContent(content);
            if (parsed.Document == null) return false;

            mutate(parsed.Document);
            File.WriteAllText(arb.FilePath, ArbSerializer.Serialize(parsed.Document));
            return true;
        }
        catch (Exception ex) {
            MessageBox.Show($"Failed to modify {arb.FilePath}: {ex.Message}", "Arb.NET", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    private void DirectoryCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
        if (DirectoryCombo.SelectedItem is string dir) {
            BuildTable(dir);
        }
    }

    private void ArbGrid_OnCellEditEnding(object sender, DataGridCellEditEndingEventArgs e) {
        if (suppressSave) return;
        if (e.EditAction != DataGridEditAction.Commit) return;

        string locale = (string)e.Column.Header;
        if (string.IsNullOrEmpty(locale) || locale == "Key") return;
        if (e.Row.Item is not ArbRow row) return;
        if (DirectoryCombo.SelectedItem is not string directory) return;

        string newValue = (e.EditingElement as TextBox)?.Text ?? string.Empty;
        SaveEntry(directory, locale, row.Key, newValue);
    }

    private void ArbGrid_OnPreviewKeyDown(object sender, KeyEventArgs e) {
        if (e.Key != Key.F2) return;
        TryRenameSelectedRow();
        e.Handled = true;
    }

    private void ArbGrid_OnColumnDisplayIndexChanged(object _, DataGridColumnEventArgs _1) {
        if (currentDirectory != null) columnSettingsService.SaveLocaleOrder(currentDirectory, ArbGrid.Columns);
    }

    private void GridContextMenu_OnOpened(object sender, RoutedEventArgs e) {
        bool hasSelection = (ArbGrid.SelectedItem ?? ArbGrid.CurrentItem) is ArbRow;
        RenameMenuItem.IsEnabled = hasSelection;
        DeleteKeyMenuItem.IsEnabled = hasSelection;
    }

    private void RenameMenuItem_OnClick(object sender, RoutedEventArgs e) {
        TryRenameSelectedRow();
    }

    private void AddKeyButton_OnClick(object sender, RoutedEventArgs e) {
        if (DirectoryCombo.SelectedItem is not string directory) return;
        if (!arbScanResult.DirGroupedArbFiles.TryGetValue(directory, out List<ArbFile> arbFiles)) return;

        ArbInputDialog dialog = new("Add ARB Key", "Enter the new key name:", "");
        dialog.ShowDialog();
        string? newKey = dialog.Result;
        if (string.IsNullOrWhiteSpace(newKey)) return;

        foreach (ArbFile arb in arbFiles) {
            ModifyArbFile(arb, doc => {
                doc.Entries[newKey!] = new ArbEntry {
                    Key = newKey!,
                    Value = ""
                };
            });
        }

        BuildTable(directory);
    }

    private void AddLocaleButton_OnClick(object sender, RoutedEventArgs e) {
        if (DirectoryCombo.SelectedItem is not string directory) return;
        if (!arbScanResult.DirGroupedArbFiles.TryGetValue(directory, out List<ArbFile> arbFiles)) return;

        ArbInputDialog dialog = new("Add ARB Locale", "Enter locale code (e.g. de, fr):", "");
        dialog.ShowDialog();
        string? locale = dialog.Result;
        if (string.IsNullOrWhiteSpace(locale)) return;
        locale = locale!.Trim();

        string filePath = Path.Combine(directory, locale + ".arb");
        if (File.Exists(filePath)) {
            MessageBox.Show($"Locale file '{Path.GetFileName(filePath)}' already exists.", "Arb.NET", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SortedSet<string> allKeys = new(StringComparer.Ordinal);
        foreach (ArbFile arb in arbFiles) {
            try {
                string content = File.ReadAllText(arb.FilePath);
                ArbParseResult result = parser.ParseContent(content);
                if (result.Document == null) continue;

                foreach (string key in result.Document.Entries.Keys) {
                    allKeys.Add(key);
                }
            }
            catch {
                // TODO handle?
            }
        }

        ArbDocument newDoc = new() {
            Locale = locale
        };
        foreach (string key in allKeys) {
            newDoc.Entries[key] = new ArbEntry {
                Key = key,
                Value = ""
            };
        }

        File.WriteAllText(filePath, ArbSerializer.Serialize(newDoc));
        _ = RefreshDataAsync();
    }

    private void RemoveKeyButton_OnClick(object sender, RoutedEventArgs e) => TryRemoveSelectedKey();

    private void RemoveKeyMenuItem_OnClick(object sender, RoutedEventArgs e) => TryRemoveSelectedKey();

    private void TryRemoveSelectedKey() {
        if ((ArbGrid.SelectedItem ?? ArbGrid.CurrentItem) is not ArbRow row) return;
        if (DirectoryCombo.SelectedItem is not string directory) return;
        if (!arbScanResult.DirGroupedArbFiles.TryGetValue(directory, out List<ArbFile> arbFiles)) return;

        string key = row.Key;
        MessageBoxResult confirm = MessageBox.Show(
            $"Remove key '{key}' from all locale files in this directory?",
            "Arb.NET \u2013 Remove Key", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        bool anyChanged = false;
        foreach (ArbFile arb in arbFiles) {
            anyChanged |= ModifyArbFile(arb, doc => doc.Entries.Remove(key));
        }

        if (anyChanged) {
            BuildTable(directory);
        }
    }

    private void SaveEntry(string directory, string locale, string key, string newValue) {
        if (!arbScanResult.DirGroupedArbFiles.TryGetValue(directory, out List<ArbFile> localeFiles)) return;

        ArbFile? arb = localeFiles.FirstOrDefault(f => string.Equals(f.LangCode, locale, StringComparison.Ordinal));
        if (arb == null) return;

        ModifyArbFile(arb, doc => {
            if (doc.Entries.TryGetValue(key, out ArbEntry entry)) {
                entry.Value = newValue;
            }
        });
    }

    private void TryRenameSelectedRow() {
        if ((ArbGrid.SelectedItem ?? ArbGrid.CurrentItem) is not ArbRow row) return;
        if (DirectoryCombo.SelectedItem is not string directory) return;

        string oldKey = row.Key;
        ArbInputDialog dialog = new("Rename ARB Key", $"Rename key '{oldKey}' in all locale files:", oldKey);
        dialog.ShowDialog();
        string? newKey = dialog.Result;
        if (newKey == oldKey || string.IsNullOrWhiteSpace(newKey)) return;

        RenameKey(directory, oldKey, newKey!);
    }

    private void RenameKey(string directory, string oldKey, string newKey) {
        if (!arbScanResult.DirGroupedArbFiles.TryGetValue(directory, out List<ArbFile> arbFiles)) return;

        bool anyChanged = false;
        foreach (ArbFile arb in arbFiles) {
            anyChanged |= ModifyArbFile(arb, doc => {
                if (!doc.Entries.TryGetValue(oldKey, out ArbEntry entry)) return;

                // Rebuild entries preserving insertion order with the new key name.
                Dictionary<string, ArbEntry> newEntries = new();
                foreach (KeyValuePair<string, ArbEntry> kvp in doc.Entries) {
                    if (kvp.Key == oldKey)
                        newEntries[newKey] = entry with {
                            Key = newKey
                        };
                    else newEntries[kvp.Key] = kvp.Value;
                }
                doc.Entries = newEntries;
            });
        }

        if (anyChanged) BuildTable(directory);
    }

    private void TranslateButton_OnClick(object sender, RoutedEventArgs e) {
        OpenTranslateDialog(currentRows.ToList());
    }

    private void TranslateSelectedMenuItem_OnClick(object sender, RoutedEventArgs e) {
        List<ArbRow> selected = ArbGrid.SelectedItems.OfType<ArbRow>().ToList();
        if (selected.Count == 0) return;
        OpenTranslateDialog(selected);
    }

    private void AiSettingsButton_OnClick(object sender, RoutedEventArgs e) {
        TranslationSettingsDialog dialog = new(translationSettingsService);
        dialog.ShowDialog();
    }

    private void OpenTranslateDialog(List<ArbRow> rows) {
        if (DirectoryCombo.SelectedItem is not string directory) return;
        if (!arbScanResult.DirGroupedArbFiles.TryGetValue(directory, out List<ArbFile> arbFiles)) return;

        TranslateDialog dialog = new(rows, currentLangCodes, arbFiles, directory, translationSettingsService, parser);
        dialog.ShowDialog();

        if (dialog.AppliedChanges) {
            BuildTable(directory);
        }
    }
}