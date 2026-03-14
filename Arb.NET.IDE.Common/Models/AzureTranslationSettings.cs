namespace Arb.NET.IDE.Common.Models;

public class AzureTranslationSettings {
    public string Endpoint { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string CustomPrompt { get; set; } = string.Empty;
    public float Temperature { get; set; }
}