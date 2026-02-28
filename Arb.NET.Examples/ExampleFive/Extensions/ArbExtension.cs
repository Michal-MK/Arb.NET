using System.Globalization;

namespace ExampleFive.Extensions;

[ContentProperty(nameof(Text))]
public class ArbExtension : IMarkupExtension {
    public string? Text { get; set; }

    public object ProvideValue(IServiceProvider serviceProvider) {
        AppLocale locale = new(new CultureInfo("en"));
        return locale.GetType().GetProperty(Text)?.GetValue(locale) ?? "<MISSING>";
    }
}