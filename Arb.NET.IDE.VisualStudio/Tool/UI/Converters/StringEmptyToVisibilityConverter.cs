using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Arb.NET.IDE.VisualStudio.Tool.UI.Converters;

/// <summary>
/// Returns Visible when the bound string is empty/null, Collapsed otherwise.
/// Set <see cref="Invert"/> to reverse: Visible when non-empty.
/// </summary>
internal sealed class StringEmptyToVisibilityConverter : IValueConverter {
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
        bool isEmpty = string.IsNullOrEmpty(value as string);
        return (isEmpty ^ Invert) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
