using System.Globalization;

namespace ExampleFive;

public partial class MainPage : ContentPage {
    public MainPage() {
        
        AppLocale l = new(new CultureInfo("en"));
        InitializeComponent();
    }
}