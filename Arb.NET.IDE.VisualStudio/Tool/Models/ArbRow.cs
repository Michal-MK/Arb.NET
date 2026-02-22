using System.Collections.Generic;

namespace Arb.NET.IDE.VisualStudio.Tool.Models;

public class ArbRow(string key) {
    public string Key => key;

    // Read by WPF DataGrid via indexer binding: Values[{locale}]
    // ReSharper disable once CollectionNeverQueried.Global
    public Dictionary<string, string> Values { get; } = new();
}