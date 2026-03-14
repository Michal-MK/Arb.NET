using System.ComponentModel;

namespace Arb.NET.IDE.VisualStudio.Tool.Models;

internal class TranslationItem : INotifyPropertyChanged {

    public string Key { get; set; } = string.Empty;
    public string SourceText { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TargetLocale { get; set; } = string.Empty;
    public string ExistingTranslation { get; set; } = string.Empty;

    public string ProposedTranslation {
        get;
        set {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProposedTranslation)));
        }
    } = string.Empty;

    public bool Accepted {
        get;
        set {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Accepted)));
        }
    } = true;

    public event PropertyChangedEventHandler? PropertyChanged;
}