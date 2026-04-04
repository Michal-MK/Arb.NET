using System.ComponentModel;

namespace Arb.NET.IDE.VisualStudio.Tool.Models;

public class PlaceholderRenameItem(string originalName) : INotifyPropertyChanged {
    public string OriginalName => originalName;

    public string NewName {
        get;
        set {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NewName)));
        }
    } = originalName;

    public event PropertyChangedEventHandler? PropertyChanged;
}