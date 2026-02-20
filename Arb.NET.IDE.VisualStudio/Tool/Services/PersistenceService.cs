using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell.Settings;

namespace Arb.NET.IDE.VisualStudio.Tool.Services;

public class PersistenceService(ArbPackage package) {
    #region Column layout persistence

    private const string SETTINGS_COLLECTION = "Arb.NET\\ColumnLayout";
    private const string ORDER_SUFFIX = ":__order__";
    private const double DEFAULT_KEY_COLUMN_WIDTH = 240;
    private const double DEFAULT_LOCALE_COLUMN_WIDTH = 220;
    
    private static string WidthKey(string directory, string header) => $"{directory}:{header}";
    private static string OrderKey(string directory) => $"{directory}{ORDER_SUFFIX}";

    #endregion
    
    private SettingsStore Store => new ShellSettingsManager(package).GetReadOnlySettingsStore(SettingsScope.UserSettings);
    private WritableSettingsStore WritableStore = new ShellSettingsManager(package).GetWritableSettingsStore(SettingsScope.UserSettings);
    
    public Dictionary<string, double> LoadColumnWidths(string directory) {
        Dictionary<string, double> widths = new(StringComparer.Ordinal);
        try {
            if (!Store.CollectionExists(SETTINGS_COLLECTION)) return widths;

            foreach (string name in Store.GetPropertyNames(SETTINGS_COLLECTION)) {
                string prefix = directory + ":";
                if (!name.StartsWith(prefix, StringComparison.Ordinal)) continue;
                string header = name.Substring(prefix.Length);
                string raw = Store.GetString(SETTINGS_COLLECTION, name, null);
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
    
    public void SaveColumnWidths(string directory, ObservableCollection<DataGridColumn> columns) {
        try {
            if (!WritableStore.CollectionExists(SETTINGS_COLLECTION)) WritableStore.CreateCollection(SETTINGS_COLLECTION);
            foreach (DataGridColumn col in columns) {
                if (col.Header is not string header) continue;

                // Prefer ActualWidth (post-layout); fall back to the Width pixel value (set before layout).
                double w = col.ActualWidth > 0 ? col.ActualWidth
                    : col.Width.IsAbsolute ? col.Width.Value
                    : 0;
                if (w > 0) WritableStore.SetString(SETTINGS_COLLECTION, WidthKey(directory, header), w.ToString("R"));
            }
        }
        catch {
            /* non-critical */
        }
    }
    
    // Locale order persisted as comma-separated locale names in display-index order (Key column excluded).
    public List<string> LoadLocaleOrder(string directory) {
        try {
            if (!Store.CollectionExists(SETTINGS_COLLECTION)) return null;
            string raw = Store.GetString(SETTINGS_COLLECTION, OrderKey(directory), null);
            if (!string.IsNullOrEmpty(raw)) return [.. raw.Split(',')];
        }
        catch {
            /* non-critical */
        }
        return null;
    }

    public void SaveLocaleOrder(string directory, ObservableCollection<DataGridColumn> columns) {
        try {
            if (!WritableStore.CollectionExists(SETTINGS_COLLECTION)) WritableStore.CreateCollection(SETTINGS_COLLECTION);

            // Collect locale columns in current display-index order (skip the Key column at index 0).
            string[] ordered = columns
                .OrderBy(c => c.DisplayIndex)
                .Select(c => c.Header as string)
                .Where(h => h != null && h != "Key")
                .ToArray();
            WritableStore.SetString(SETTINGS_COLLECTION, OrderKey(directory), string.Join(",", ordered));
        }
        catch {
            /* non-critical */
        }
    }
}