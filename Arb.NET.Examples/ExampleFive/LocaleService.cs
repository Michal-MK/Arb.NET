using System.Globalization;
using Arb.NET;

namespace ExampleFive;

/// <summary>
/// Holds the active locale and raises <see cref="CultureChanged"/> when the culture is switched.
/// </summary>
public class LocaleService : IArbLocale {
    private AppLocale locale;

    public event EventHandler? CultureChanged;

    public LocaleService() {
        locale = new AppLocale(new CultureInfo("en"));
    }

    public string? this[string? key] => locale[key];

    public void SetCulture(CultureInfo culture) {
        locale = new AppLocale(culture);
        CultureChanged?.Invoke(this, EventArgs.Empty);
    }
}