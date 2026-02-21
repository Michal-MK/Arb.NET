namespace Arb.NET.IDE.Common.Models;

public class AzureTranslationSettings {
    public string Endpoint { get; set; } = "";
    public string DeploymentName { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string CustomPrompt { get; set; } = "";
    public float Temperature { get; set; }
}