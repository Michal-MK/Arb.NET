namespace ExampleFive.Extensions;

[ContentProperty(nameof(Text))]
public class ArbExtension : IMarkupExtension {
    public string? Text { get; set; }

    public object ProvideValue(IServiceProvider serviceProvider) {
        IServiceProvider? appServices = Application.Current?.Handler?.MauiContext?.Services;
        LocaleService? localeService = appServices?.GetService<LocaleService>();

        if (localeService is not null
            && serviceProvider.GetService<IProvideValueTarget>() is { } pvt
            && pvt.TargetObject is BindableObject target
            && pvt.TargetProperty is BindableProperty property) {

            WeakReference<BindableObject> weakTarget = new(target);
            EventHandler? handler = null;
            handler = (_, _) => {
                if (weakTarget.TryGetTarget(out BindableObject? t)) {
                    string key = Text ?? string.Empty;
                    MainThread.BeginInvokeOnMainThread(
                        () => t.SetValue(property, localeService[key] ?? $"<MISSING '{key}'>")
                    );
                } else {
                    localeService.CultureChanged -= handler;
                }
            };
            localeService.CultureChanged += handler;
        }

        return localeService?[Text] ?? $"<MISSING '{Text}'>";
    }
}
