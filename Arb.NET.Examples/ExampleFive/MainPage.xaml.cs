using System.Globalization;

namespace ExampleFive;

public partial class MainPage : ContentPage {
    private readonly LocaleService localeService;

    public MainPage(LocaleService localeService) {
        this.localeService = localeService;
        InitializeComponent();
    }

    private void OnEnClicked(object sender, EventArgs e) => SwitchLocale("en");
    private void OnCsClicked(object sender, EventArgs e) => SwitchLocale("cs");

    private void SwitchLocale(string culture) {
        localeService.SetCulture(new CultureInfo(culture));
    }
}