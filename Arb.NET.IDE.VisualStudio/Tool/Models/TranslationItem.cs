using System.ComponentModel;

namespace Arb.NET.IDE.VisualStudio.Tool.Models;

internal class TranslationItem : INotifyPropertyChanged {

    public string Key { get; set; } = "";
    public string SourceText { get; set; } = "";
    public string? Description { get; set; }
    public string TargetLocale { get; set; } = "";
    public string ExistingTranslation { get; set; } = "";

    public string ProposedTranslation {
        get;
        set {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProposedTranslation)));
        }
    } = "";

    public bool Accepted {
        get;
        set {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Accepted)));
        }
    } = true;

    public event PropertyChangedEventHandler? PropertyChanged;
}