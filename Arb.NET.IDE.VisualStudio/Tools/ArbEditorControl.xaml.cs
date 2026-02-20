using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.Settings;
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
using System.Windows.Input;
using EnvDTE;

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

    private const string SettingsCollection = "Arb.NET\\ColumnLayout";
    private const string OrderSuffix = ":__order__";
    private const double DefaultKeyColumnWidth = 240;
    private const double DefaultLocaleColumnWidth = 220;

    private AsyncPackage package;
    private Dictionary<string, List<ArbFile>> byDirectory = new();
    private List<string> currentLangCodes = [];
    private ObservableCollection<ArbRow> currentRows = [];
    private string currentDirectory;
    private bool suppressSave;
    private uint solutionEventsCookie;
    private readonly List<(DependencyPropertyDescriptor Dpd, DataGridColumn Col, EventHandler Handler)> widthListeners = [];

    private readonly ArbParser parser = new();

    public ArbEditorControl() {
        InitializeComponent();
        ArbGrid.ColumnDisplayIndexChanged += ArbGrid_OnColumnDisplayIndexChanged;
        Unloaded += (_, _) => UnsubscribeSolutionEvents();
    }

    public void Initialize(AsyncPackage vsPackage) {
        package = vsPackage;
        SubscribeSolutionEvents();
        _ = LoadDataAsync();
    }

    // Called by SolutionEventSink — avoids discard-vs-parameter name collision inside the nested class.
    private void TriggerLoad() => _ = LoadDataAsync();

    private void SubscribeSolutionEvents() {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (((IServiceProvider)package).GetService(typeof(SVsSolution)) is IVsSolution solution)
            solution.AdviseSolutionEvents(new SolutionEventSink(this), out solutionEventsCookie);
    }

    private void UnsubscribeSolutionEvents() {
        if (package == null || solutionEventsCookie == 0) return;
        ThreadHelper.ThrowIfNotOnUIThread();
        if (((IServiceProvider)package).GetService(typeof(SVsSolution)) is IVsSolution solution)
            solution.UnadviseSolutionEvents(solutionEventsCookie);
        solutionEventsCookie = 0;
    }

    // Minimal IVsSolutionEvents sink that reloads data when a solution is opened or closed.
#pragma warning disable IDE0060 // Remove unused parameter — COM interface requires named parameters
    private sealed class SolutionEventSink(ArbEditorControl owner) : IVsSolutionEvents {
        public int OnAfterOpenSolution(object pUnkReserved, int fNewSolution) {
            owner.TriggerLoad();
            return VSConstants.S_OK;
        }

        public int OnAfterCloseSolution(object pUnkReserved) {
            owner.TriggerLoad();
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
    }
#pragma warning restore IDE0060

    private Task<(Dictionary<string, List<ArbFile>> ByDir, List<Exception> Errors)> ScanArbFilesAsync(string solutionDir) {
        return Task.Run(() => {
            Dictionary<string, List<ArbFile>> byDir = new(StringComparer.OrdinalIgnoreCase);
            List<Exception> errors = [];

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
                    errors.Add(ex);
                }
            }

            return (byDir, errors);
        });
    }

    private async Task LoadDataAsync() {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        string solutionDir = GetSolutionDirectory();

        if (string.IsNullOrEmpty(solutionDir)) {
            LoadingLabel.Text = "No solution open. Open a solution and reopen this window.";
            return;
        }

        (Dictionary<string, List<ArbFile>> byDir, List<Exception> errors) = await ScanArbFilesAsync(solutionDir);

        if (errors.Count > 0) {
            MessageBox.Show($"Completed with {errors.Count} error(s):\n" +
                            string.Join("\n", errors.Select(ex => $"  {ex.Message}")),
                            "Arb.NET - Exception", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        byDirectory = byDir;

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

    public void RefreshData() {
        if (package == null) return;
        _ = RefreshDataAsync();
    }

    private async Task RefreshDataAsync() {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        string solutionDir = GetSolutionDirectory();
        if (string.IsNullOrEmpty(solutionDir)) return;

        (Dictionary<string, List<ArbFile>> byDir, List<Exception> errors) = await ScanArbFilesAsync(solutionDir);

        if (errors.Count > 0) {
            MessageBox.Show($"Completed refresh with {errors.Count} errors:\n" +
                            string.Join("\n", errors.Select(ex => $"  {ex.Message}")),
                            "Arb.NET - Refresh Errors", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        byDirectory = byDir;

        if (DirectoryCombo.SelectedItem is string currentDir && byDirectory.ContainsKey(currentDir)) {
            BuildTable(currentDir);
        }
        else if (byDirectory.Count > 0) {
            List<string> sortedDirs = byDirectory.Keys.OrderBy(d => d).ToList();
            DirectoryCombo.ItemsSource = sortedDirs;
            DirectoryCombo.SelectedIndex = 0;
        }
    }

    // ── Column width persistence ───────────────────────────────────────────────

    // Settings keys are scoped by directory so different directories don't share widths.
    private static string WidthKey(string directory, string header) => $"{directory}:{header}";
    private static string OrderKey(string directory) => $"{directory}{OrderSuffix}";

    private Dictionary<string, double> LoadColumnWidths(string directory) {
        Dictionary<string, double> widths = new(StringComparer.Ordinal);
        try {
            SettingsStore store = new ShellSettingsManager(package).GetReadOnlySettingsStore(SettingsScope.UserSettings);
            if (!store.CollectionExists(SettingsCollection)) return widths;

            foreach (string name in store.GetPropertyNames(SettingsCollection)) {
                string prefix = directory + ":";
                if (!name.StartsWith(prefix, StringComparison.Ordinal)) continue;
                string header = name.Substring(prefix.Length);
                string raw = store.GetString(SettingsCollection, name, null);
                if (double.TryParse(raw, out double w) && w > 0) {
                    widths[header] = w;
                }
            }
        }
        catch {
            /* non-critical — fall back to defaults */
        }
        return widths;
    }

    private void SaveColumnWidths(string directory) {
        try {
            WritableSettingsStore store = new ShellSettingsManager(package).GetWritableSettingsStore(SettingsScope.UserSettings);
            if (!store.CollectionExists(SettingsCollection)) store.CreateCollection(SettingsCollection);
            foreach (DataGridColumn col in ArbGrid.Columns) {
                if (col.Header is not string header) continue;
                // Prefer ActualWidth (post-layout); fall back to the Width pixel value (set before layout).
                double w = col.ActualWidth > 0 ? col.ActualWidth
                         : col.Width.IsAbsolute ? col.Width.Value
                         : 0;
                if (w > 0) store.SetString(SettingsCollection, WidthKey(directory, header), w.ToString("R"));
            }
        }
        catch {
            /* non-critical */
        }
    }

    // Locale order persisted as comma-separated locale names in display-index order (Key column excluded).
    private List<string> LoadLocaleOrder(string directory) {
        try {
            SettingsStore store = new ShellSettingsManager(package).GetReadOnlySettingsStore(SettingsScope.UserSettings);
            if (!store.CollectionExists(SettingsCollection)) return null;
            string raw = store.GetString(SettingsCollection, OrderKey(directory), null);
            if (!string.IsNullOrEmpty(raw)) return [.. raw.Split(',')];
        }
        catch {
            /* non-critical */
        }
        return null;
    }

    private void SaveLocaleOrder(string directory) {
        try {
            WritableSettingsStore store = new ShellSettingsManager(package).GetWritableSettingsStore(SettingsScope.UserSettings);
            if (!store.CollectionExists(SettingsCollection)) store.CreateCollection(SettingsCollection);

            // Collect locale columns in current display-index order (skip the Key column at index 0).
            string[] ordered = ArbGrid.Columns
                .OrderBy(c => c.DisplayIndex)
                .Select(c => c.Header as string)
                .Where(h => h != null && h != "Key")
                .ToArray();
            store.SetString(SettingsCollection, OrderKey(directory), string.Join(",", ordered));
        }
        catch {
            /* non-critical */
        }
    }

    private void BuildTable(string directory) {
        if (!byDirectory.TryGetValue(directory, out List<ArbFile> arbFiles)) return;

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
        List<string> savedOrder = LoadLocaleOrder(directory);
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
        currentDirectory = directory;

        Dictionary<string, double> savedWidths = LoadColumnWidths(directory);

        // Detach previous width listeners before clearing columns.
        DependencyPropertyDescriptor widthDpd = DependencyPropertyDescriptor.FromProperty(DataGridColumn.WidthProperty, typeof(DataGridColumn));
        foreach ((DependencyPropertyDescriptor dpd, DataGridColumn col, EventHandler handler) in widthListeners)
            dpd.RemoveValueChanged(col, handler);
        widthListeners.Clear();

        ArbGrid.Columns.Clear();
        ArbGrid.Columns.Add(new DataGridTextColumn {
            Header = "Key",
            Binding = new Binding("Key"),
            IsReadOnly = true,
            Width = savedWidths.TryGetValue("Key", out double kw) ? kw : DefaultKeyColumnWidth
        });
        foreach (string locale in currentLangCodes) {
            ArbGrid.Columns.Add(new DataGridTextColumn {
                Header = locale,
                Binding = new Binding($"Values[{locale}]"),
                Width = savedWidths.TryGetValue(locale, out double lw) ? lw : DefaultLocaleColumnWidth
            });
        }

        ArbGrid.ItemsSource = currentRows;
        suppressSave = false;

        // Attach width-change listeners after suppressSave is cleared so the initial layout fires are ignored.
        foreach (DataGridColumn col in ArbGrid.Columns) {
            void Handler(object _, EventArgs _1) {
                if (!suppressSave && currentDirectory != null) SaveColumnWidths(currentDirectory);
            }
            widthDpd.AddValueChanged(col, Handler);
            widthListeners.Add((widthDpd, col, Handler));
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

        string locale = e.Column.Header as string;
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

    private void ArbGrid_OnColumnWidthChanged(object _, DataGridColumnEventArgs _1) {
        if (suppressSave) return;
        if (currentDirectory != null) SaveColumnWidths(currentDirectory);
    }

    private void ArbGrid_OnColumnDisplayIndexChanged(object _, DataGridColumnEventArgs _1) {
        if (currentDirectory != null) SaveLocaleOrder(currentDirectory);
    }

    private void GridContextMenu_OnOpened(object sender, RoutedEventArgs e) {
        RenameMenuItem.IsEnabled = (ArbGrid.SelectedItem ?? ArbGrid.CurrentItem) is ArbRow;
    }

    private void RenameMenuItem_OnClick(object sender, RoutedEventArgs e) {
        TryRenameSelectedRow();
    }

    private void AddKeyButton_OnClick(object sender, RoutedEventArgs e) {
        if (DirectoryCombo.SelectedItem is not string directory) return;
        if (!byDirectory.TryGetValue(directory, out List<ArbFile> arbFiles)) return;

        ArbInputDialog dialog = new("Add ARB Key", "Enter the new key name:", "");
        dialog.ShowDialog();
        string newKey = dialog.Result;
        if (newKey == null || string.IsNullOrWhiteSpace(newKey)) return;

        foreach (ArbFile arb in arbFiles) {
            ModifyArbFile(arb, doc => {
                doc.Entries[newKey] = new ArbEntry {
                    Key = newKey,
                    Value = ""
                };
            });
        }

        BuildTable(directory);
    }

    private void SaveEntry(string directory, string locale, string key, string newValue) {
        if (!byDirectory.TryGetValue(directory, out List<ArbFile> localeFiles)) return;

        ArbFile arb = localeFiles.FirstOrDefault(f => string.Equals(f.LangCode, locale, StringComparison.Ordinal));
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
        string newKey = dialog.Result;
        if (newKey == null || newKey == oldKey || string.IsNullOrWhiteSpace(newKey)) return;

        RenameKey(directory, oldKey, newKey);
    }

    private void RenameKey(string directory, string oldKey, string newKey) {
        if (!byDirectory.TryGetValue(directory, out List<ArbFile> arbFiles)) return;

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

    private string GetSolutionDirectory() {
        ThreadHelper.ThrowIfNotOnUIThread();
        DTE dte = ((IServiceProvider)package).GetService(typeof(DTE)) as DTE;
        string solutionFullPath = dte?.Solution?.FullName;
        return string.IsNullOrEmpty(solutionFullPath)
            ? null
            : Path.GetDirectoryName(solutionFullPath);
    }
}