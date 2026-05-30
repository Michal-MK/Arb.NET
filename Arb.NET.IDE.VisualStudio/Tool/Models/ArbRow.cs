using System.Collections.Generic;

namespace Arb.NET.IDE.VisualStudio.Tool.Models;

public class ArbRow(string key) {
    public string Key => key;

    // Read by WPF DataGrid via indexer binding: Values[{locale}]
    // ReSharper disable once CollectionNeverQueried.Global
    public Dictionary<string, string> Values { get; } = new();

    // Fallback preview text shown when Values[locale] is empty but a parent locale has a value.
    // Format: "(parentLocale) value". Empty string means no fallback available.
    // ReSharper disable once CollectionNeverQueried.Global
    public Dictionary<string, string> Previews { get; } = new();
}