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
using System.Windows.Input;
using EnvDTE;
using Microsoft.VisualStudio.PlatformUI;

namespace Arb.NET.IDE.VisualStudio.Tools;

public partial class ArbEditorControl : UserControl {

    private sealed class ArbFile {
        public string LangCode { get; }
        public string FilePath { get; }

        public ArbFile(string langCode, string filePath) {
            LangCode = langCode;
            FilePath = filePath;
        }
    }

    private class ArbRow {
        public string Key { get; set; }

        // Read by WPF DataGrid via indexer binding: Values[{locale}]
        // ReSharper disable once CollectionNeverQueried.Local
        public Dictionary<string, string> Values { get; } = new();
    }

    private AsyncPackage package;
    private Dictionary<string, List<ArbFile>> byDirectory = new();
    private List<string> currentLangCodes = [];

    private ObservableCollection<ArbRow> currentRows = [];
    private bool suppressSave;

    public ArbEditorControl() {
        InitializeComponent();
    }

    public void Initialize(AsyncPackage vsPackage) {
        package = vsPackage;
        _ = LoadDataAsync();
    }

    private async Task LoadDataAsync() {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        string solutionDir = GetSolutionDirectory();

        if (string.IsNullOrEmpty(solutionDir)) {
            LoadingLabel.Text = "No solution open. Open a solution and reopen this window.";
            return;
        }

        await Task.Run(() => {
            Dictionary<string, List<ArbFile>> byDir = new(StringComparer.OrdinalIgnoreCase);
            ArbParser parser = new();

            foreach (string filePath in Directory.EnumerateFiles(solutionDir, "*.arb", SearchOption.AllDirectories)) {
                try {
                    string content = File.ReadAllText(filePath);
                    ArbParseResult result = parser.ParseContent(content);
                    if (result.Document == null) continue;

                    ArbDocument doc = result.Document;
                    string locale = string.IsNullOrEmpty(doc.Locale)
                        ? Path.GetFileNameWithoutExtension(filePath)
                        : doc.Locale;

                    string dir = Path.GetDirectoryName(filePath) ?? solutionDir;

                    if (!byDir.TryGetValue(dir, out List<ArbFile> list)) {
                        list = [];
                        byDir[dir] = list;
                    }
                    list.Add(new ArbFile(locale, filePath));
                }
                catch (Exception ex) {
                    MessageBox.Show(ex.Message, "Arb.NET - Exception", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            byDirectory = byDir;
        });

        if (byDirectory.Count == 0) {
            LoadingLabel.Text = "No .arb files found in this solution.";
            return;
        }

        LoadingLabel.Visibility = Visibility.Collapsed;
        ArbGrid.Visibility = Visibility.Visible;

        List<string> sortedDirs = byDirectory.Keys.OrderBy(d => d).ToList();
        DirectoryCombo.ItemsSource = sortedDirs;
        DirectoryCombo.SelectedIndex = 0;
    }

    private string GetSolutionDirectory() {
        ThreadHelper.ThrowIfNotOnUIThread();
        DTE dte = ((IServiceProvider)package).GetService(typeof(DTE)) as DTE;
        string solutionFullPath = dte?.Solution?.FullName;
        return string.IsNullOrEmpty(solutionFullPath)
            ? null
            : Path.GetDirectoryName(solutionFullPath);
    }

    private void BuildTable(string directory) {
        if (!byDirectory.TryGetValue(directory, out List<ArbFile> arbFiles)) return;

        suppressSave = true;

        // Collect all keys from all locale files in this directory.
        SortedSet<string> allKeys = new(StringComparer.Ordinal);
        Dictionary<string, Dictionary<string, string>> byLocale = new(StringComparer.Ordinal);

        foreach (ArbFile arb in arbFiles) {
            try {
                string content = File.ReadAllText(arb.FilePath);
                ArbParseResult result = new ArbParser().ParseContent(content);

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

        currentLangCodes = arbFiles.Select(lf => lf.LangCode).OrderBy(l => l).ToList();

        ObservableCollection<ArbRow> rows = [];
        foreach (string key in allKeys) {
            ArbRow row = new() {
                Key = key
            };
            foreach (string locale in currentLangCodes) {
                row.Values[locale] = byLocale.TryGetValue(locale, out var vals) && vals.TryGetValue(key, out string val)
                    ? val
                    : string.Empty;
            }
            rows.Add(row);
        }
        currentRows = rows;

        ArbGrid.Columns.Clear();

        // Key column — read-only.
        ArbGrid.Columns.Add(new DataGridTextColumn {
            Header = "Key",
            Binding = new Binding("Key"),
            IsReadOnly = true,
            Width = 240
        });

        // One editable column per locale.
        foreach (string locale in currentLangCodes) {
            ArbGrid.Columns.Add(new DataGridTextColumn {
                Header = locale,
                Binding = new Binding($"Values[{locale}]"),
                Width = 220
            });
        }

        ArbGrid.ItemsSource = currentRows;
        suppressSave = false;
    }

    private void DirectoryCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
        if (DirectoryCombo.SelectedItem is string dir) {
            BuildTable(dir);
        }
    }

    private void ArbGrid_OnCellEditEnding(object sender, DataGridCellEditEndingEventArgs e) {
        if (suppressSave) return;
        if (e.EditAction != DataGridEditAction.Commit) return;
        if (e.Column.DisplayIndex == 0) return; // Key column — rename only via dialog.

        if (e.Row.Item is not ArbRow row) return;
        if (DirectoryCombo.SelectedItem is not string directory) return;

        string locale = currentLangCodes[e.Column.DisplayIndex - 1];
        string newValue = (e.EditingElement as TextBox)?.Text ?? string.Empty;

        SaveEntry(directory, locale, row.Key, newValue);
    }

    private void ArbGrid_OnPreviewKeyDown(object sender, KeyEventArgs e) {
        if (e.Key != Key.F2) return;

        TryRenameSelectedRow();
        e.Handled = true;
    }

    private void GridContextMenu_OnOpened(object sender, RoutedEventArgs e) {
        RenameMenuItem.IsEnabled = (ArbGrid.SelectedItem ?? ArbGrid.CurrentItem) is ArbRow;
    }

    private void RenameMenuItem_OnClick(object sender, RoutedEventArgs e) {
        TryRenameSelectedRow();
    }

    private void SaveEntry(string directory, string locale, string key, string newValue) {
        if (!byDirectory.TryGetValue(directory, out List<ArbFile> localeFiles)) return;

        ArbFile arb = localeFiles.FirstOrDefault(f => string.Equals(f.LangCode, locale, StringComparison.Ordinal));
        if (arb == null) return;

        try {
            string content = File.ReadAllText(arb.FilePath);
            ArbParseResult parsed = new ArbParser().ParseContent(content);
            if (parsed.Document == null) return;

            if (!parsed.Document.Entries.TryGetValue(key, out ArbEntry entry)) return;

            entry.Value = newValue;
            File.WriteAllText(arb.FilePath, ArbSerializer.Serialize(parsed.Document));
        }
        catch (Exception ex) {
            MessageBox.Show($"Failed to save entry '{key}': {ex.Message}", "Arb.NET", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void TryRenameSelectedRow() {
        if ((ArbGrid.SelectedItem ?? ArbGrid.CurrentItem) is not ArbRow row) return;
        if (DirectoryCombo.SelectedItem is not string directory) return;

        string oldKey = row.Key;
        string newKey = ShowRenameDialog(oldKey);
        if (newKey == null || newKey == oldKey || string.IsNullOrWhiteSpace(newKey)) return;

        RenameKey(directory, oldKey, newKey);
    }

    private void RenameKey(string directory, string oldKey, string newKey) {
        if (!byDirectory.TryGetValue(directory, out List<ArbFile> arbFiles)) return;

        bool anyChanged = false;
        foreach (ArbFile arb in arbFiles) {
            try {
                string content = File.ReadAllText(arb.FilePath);
                ArbParseResult parsed = new ArbParser().ParseContent(content);
                if (parsed.Document == null) continue;

                ArbDocument doc = parsed.Document;
                if (!doc.Entries.TryGetValue(oldKey, out ArbEntry entry)) continue;

                // Rebuild entries preserving insertion order with the new key name.
                Dictionary<string, ArbEntry> newEntries = new();
                foreach (KeyValuePair<string, ArbEntry> kvp in doc.Entries) {
                    if (kvp.Key == oldKey) {
                        newEntries[newKey] = entry with {
                            Key = newKey
                        };
                    }
                    else {
                        newEntries[kvp.Key] = kvp.Value;
                    }
                }
                doc.Entries = newEntries;
                File.WriteAllText(arb.FilePath, ArbSerializer.Serialize(doc));
                anyChanged = true;
            }
            catch (Exception ex) {
                MessageBox.Show($"Failed to rename key in {arb.FilePath}: {ex.Message}", "Arb.NET", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        if (anyChanged) {
            BuildTable(directory);
        }
    }

    private static string ShowRenameDialog(string currentKey) {
        // DialogWindow is the VS-themed dialog base — it picks up the current VS colour theme
        // automatically (background, foreground, borders) without any manual brush wiring.
        DialogWindow dialog = new() {
            Title = "Rename ARB Key",
            Width = 400,
            Height = 140,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            HasMaximizeButton = false,
            HasMinimizeButton = false
        };

        Grid grid = new() {
            Margin = new Thickness(12)
        };
        grid.RowDefinitions.Add(new RowDefinition {
            Height = GridLength.Auto
        });
        grid.RowDefinitions.Add(new RowDefinition {
            Height = GridLength.Auto
        });
        grid.RowDefinitions.Add(new RowDefinition {
            Height = GridLength.Auto
        });

        TextBlock label = new() {
            Text = $"Rename key '{currentKey}' in all locale files:",
            Margin = new Thickness(0, 0, 0, 6)
        };
        Grid.SetRow(label, 0);

        TextBox textBox = new() {
            Text = currentKey,
            Margin = new Thickness(0, 0, 0, 10)
        };
        Grid.SetRow(textBox, 1);

        StackPanel buttons = new() {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetRow(buttons, 2);

        string result = null;
        Button ok = new() {
            Content = "OK",
            Width = 75,
            Margin = new Thickness(0, 0, 8, 0),
            IsDefault = true
        };
        ok.Click += (_, _) => {
            result = textBox.Text;
            dialog.DialogResult = true;
        };

        Button cancel = new() {
            Content = "Cancel",
            Width = 75,
            IsCancel = true
        };

        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        grid.Children.Add(label);
        grid.Children.Add(textBox);
        grid.Children.Add(buttons);

        dialog.Content = grid;

        textBox.Loaded += (_, _) => {
            textBox.SelectAll();
            textBox.Focus();
        };

        dialog.ShowDialog();
        return result;
    }
}