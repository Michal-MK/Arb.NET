using System;
using System.Windows.Data;
using Arb.NET.IDE.VisualStudio.Tool.Models;

namespace Arb.NET.IDE.VisualStudio.Tool.UI.Converters;

/// <summary>Converts an absolute directory path to a solution-relative path for display.</summary>
internal sealed class RelativePathConverter : IValueConverter {
    public ArbScanResult? ScanResult { get; set; }

    public object Convert(object? value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
        return value is string abs && ScanResult != null ? ScanResult.RelativePath(abs) : value ?? string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
        throw new NotSupportedException();
    }
}