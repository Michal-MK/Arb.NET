using System.Globalization;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ExampleFive;

public partial class MainPage : ContentPage {
    private readonly LocaleService localeService;

    public MainPage(LocaleService localeService) {
        this.localeService = localeService;
        BindingContext = new MainPageVm();
        InitializeComponent();
    }

    private void OnEnClicked(object sender, EventArgs e) => SwitchLocale("en");
    private void OnCsClicked(object sender, EventArgs e) => SwitchLocale("cs");

    private void SwitchLocale(string culture) {
        localeService.SetCulture(new CultureInfo(culture));
    }
}

public partial class MainPageVm : ObservableObject {
    public ICommand RawICommandCommand { get; set; }

    [ObservableProperty]
    public partial string SomeObservableProperty { get; set; } = "Init";

    public string? SomeStringBinding { get; set; }

    public MainPageVm() {
        RawICommandCommand = new Command(RawICommandAction);
    }
    
    [RelayCommand]
    private void Test() {
        Console.WriteLine("Testing command binding navigation from generated code in Rider...");
    }

    private void RawICommandAction() {
        SomeObservableProperty += "!";
    }
}