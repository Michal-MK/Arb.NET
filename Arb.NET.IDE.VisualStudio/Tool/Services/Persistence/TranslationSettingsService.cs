using Arb.NET.IDE.Common.Models;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell.Settings;

namespace Arb.NET.IDE.VisualStudio.Tool.Services.Persistence;

public class TranslationSettingsService(ArbPackage package) {
    private const string SETTINGS_COLLECTION = "Arb.NET\\TranslationSettings";

    private SettingsStore Store => new ShellSettingsManager(package).GetReadOnlySettingsStore(SettingsScope.UserSettings);
    private WritableSettingsStore WritableStore => new ShellSettingsManager(package).GetWritableSettingsStore(SettingsScope.UserSettings);

    public AzureTranslationSettings Load() {
        AzureTranslationSettings settings = new();
        try {
            if (!Store.CollectionExists(SETTINGS_COLLECTION)) return settings;
            settings.Endpoint = Store.GetString(SETTINGS_COLLECTION, "Endpoint", "");
            settings.DeploymentName = Store.GetString(SETTINGS_COLLECTION, "DeploymentName", "");
            settings.ApiKey = Store.GetString(SETTINGS_COLLECTION, "ApiKey", "");
            settings.CustomPrompt = Store.GetString(SETTINGS_COLLECTION, "CustomPrompt", "");
            string tempStr = Store.GetString(SETTINGS_COLLECTION, "Temperature", "0.3");
            if (float.TryParse(tempStr, out float temp)) settings.Temperature = temp;
        }
        catch {
            /* non-critical */
        }
        return settings;
    }

    public void Save(AzureTranslationSettings settings) {
        try {
            if (!WritableStore.CollectionExists(SETTINGS_COLLECTION))
                WritableStore.CreateCollection(SETTINGS_COLLECTION);
            WritableStore.SetString(SETTINGS_COLLECTION, "Endpoint", settings.Endpoint ?? "");
            WritableStore.SetString(SETTINGS_COLLECTION, "DeploymentName", settings.DeploymentName ?? "");
            WritableStore.SetString(SETTINGS_COLLECTION, "ApiKey", settings.ApiKey ?? "");
            WritableStore.SetString(SETTINGS_COLLECTION, "CustomPrompt", settings.CustomPrompt ?? "");
            WritableStore.SetString(SETTINGS_COLLECTION, "Temperature", settings.Temperature.ToString("R"));
        }
        catch {
            /* non-critical */
        }
    }
}