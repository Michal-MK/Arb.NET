using System.ComponentModel;

namespace Arb.NET.IDE.VisualStudio.Tool.Models;

public class LocaleSelection(string locale) : INotifyPropertyChanged {
    public string Locale => locale;

    public bool IsSelected {
        get;
        set {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}