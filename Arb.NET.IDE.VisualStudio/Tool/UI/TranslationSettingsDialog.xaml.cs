using Arb.NET.IDE.Common.Models;
using Microsoft.VisualStudio.PlatformUI;
using System.Windows;
using Arb.NET.IDE.VisualStudio.Tool.Services.Persistence;

namespace Arb.NET.IDE.VisualStudio.Tool.UI;

public partial class TranslationSettingsDialog : DialogWindow {
    private readonly TranslationSettingsService settingsService;

    public TranslationSettingsDialog(TranslationSettingsService settingsService) {
        this.settingsService = settingsService;
        InitializeComponent();

        Title = "AI Translation Settings";
        Width = 450;
        Height = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        HasMaximizeButton = false;
        HasMinimizeButton = false;

        AzureTranslationSettings settings = settingsService.Load();
        EndpointBox.Text = settings.Endpoint;
        DeploymentBox.Text = settings.DeploymentName;
        ApiKeyBox.Password = settings.ApiKey;
        TemperatureBox.Text = settings.Temperature.ToString("0.0");
    }

    private void OkButton_OnClick(object sender, RoutedEventArgs e) {
        if (!float.TryParse(TemperatureBox.Text, out float temp)) temp = 0.3f;

        AzureTranslationSettings settings = new() {
            Endpoint = EndpointBox.Text.Trim().TrimEnd('/'),
            DeploymentName = DeploymentBox.Text.Trim(),
            ApiKey = ApiKeyBox.Password,
            Temperature = temp
        };

        settingsService.Save(settings);
        DialogResult = true;
    }
}