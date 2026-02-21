using System.Text;
using System.Net.Http;
using System.Text.Json;
using Arb.NET.IDE.Common.Models;

namespace Arb.NET.IDE.Common.Services;

// ReSharper disable once InconsistentNaming
public sealed class AzureOpenAITranslator {
    private const string API_VERSION = "2024-06-01";
    private const int MAX_RETRIES = 5;

    public (bool Valid, string? Error) ValidateSettings(AzureTranslationSettings settings) {
        if (string.IsNullOrWhiteSpace(settings.Endpoint))
            return (false, "Azure OpenAI endpoint URL is not configured.");
        if (string.IsNullOrWhiteSpace(settings.DeploymentName))
            return (false, "Deployment name is not configured.");
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
            return (false, "API key is not configured.");
        return (true, null);
    }

    public async Task<IReadOnlyList<string>> TranslateBatchAsync(
        AzureTranslationSettings settings,
        string sourceLocale,
        string targetLocale,
        IReadOnlyList<AzureTranslationItem> items,
        CancellationToken cancellationToken) {

        if (items.Count == 0) {
            return [];
        }

        using HttpClient client = CreateHttpClient(settings);
        (string systemMessage, string userMessage) = BuildPrompt(settings, sourceLocale, targetLocale, items);

        string requestJson = BuildRequestJson(settings, systemMessage, userMessage);
        string endpointUri = $"/openai/deployments/{Uri.EscapeDataString(settings.DeploymentName)}/chat/completions?api-version={API_VERSION}";

        int retries = 0;
        while (!cancellationToken.IsCancellationRequested) {
            using StringContent content = new(requestJson, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await client.PostAsync(endpointUri, content, cancellationToken).ConfigureAwait(false);

            if ((int)response.StatusCode == 429) {
                if (retries >= MAX_RETRIES) {
                    throw new HttpRequestException($"Rate limited after {MAX_RETRIES} retries.");
                }

                int backoffSeconds = 1 << retries++;
                await Task.Delay(backoffSeconds * 1000, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (!response.IsSuccessStatusCode) {
                string errorBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new HttpRequestException($"Azure OpenAI request failed: {(int)response.StatusCode} {response.ReasonPhrase}. Response: {errorBody}");
            }

            string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return ParseResponse(responseBody, items.Count);
        }

        throw new OperationCanceledException();
    }

    private static (string systemMessage, string userMessage) BuildPrompt(
        AzureTranslationSettings settings,
        string sourceLocale,
        string targetLocale,
        IReadOnlyList<AzureTranslationItem> items) {

        string systemMessage =
            $"You are a professional translator fluent in all languages, able to understand and convey both literal and nuanced meanings. " +
            $"You are an expert in the target language \"{targetLocale}\", adapting the style and tone appropriately.";

        StringBuilder userBuilder = new();

        userBuilder.AppendLine($"Translate the {items.Count} item(s) described by the following JSON array from \"{sourceLocale}\" into the target language \"{targetLocale}\".");
        userBuilder.AppendLine();

        if (!string.IsNullOrWhiteSpace(settings.CustomPrompt)) {
            userBuilder.AppendLine(settings.CustomPrompt);
            userBuilder.AppendLine();
        }

        userBuilder.AppendLine("Items to translate:");

        using MemoryStream ms = new();
        using Utf8JsonWriter writer = new(ms, new JsonWriterOptions {
            Indented = true
        });

        writer.WriteStartArray();
        foreach (AzureTranslationItem item in items) {
            writer.WriteStartObject();
            writer.WriteString("key", item.Key);
            writer.WriteString("source", item.SourceText);
            if (!string.IsNullOrEmpty(item.Description)) {
                writer.WriteString("description", item.Description);
            }
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.Flush();
        userBuilder.AppendLine(Encoding.UTF8.GetString(ms.ToArray()));

        userBuilder.AppendLine();
        userBuilder.AppendLine("IMPORTANT RULES:");
        userBuilder.AppendLine($"- Respond ONLY with a JSON array of {items.Count} translated string(s), in the same order as the source items.");
        userBuilder.AppendLine("- Do NOT include keys, descriptions, or any other fields -- only the flat array of translated strings.");
        userBuilder.AppendLine("- Preserve any placeholders like {count}, {name}, {0}, etc. exactly as they appear in the source.");
        userBuilder.AppendLine("- Preserve any ICU message format syntax (plural, select, etc.) structure exactly.");
        userBuilder.AppendLine("- Do not add any explanation or commentary.");
        userBuilder.AppendLine();
        userBuilder.AppendLine("Expected response format: [\"translated text 1\", \"translated text 2\"]");

        return (systemMessage, userBuilder.ToString());
    }

    private static string BuildRequestJson(AzureTranslationSettings settings, string systemMessage, string userMessage) {
        using MemoryStream ms = new();
        using Utf8JsonWriter writer = new(ms);
        writer.WriteStartObject();

        writer.WritePropertyName("messages");
        writer.WriteStartArray();

        writer.WriteStartObject();
        writer.WriteString("role", "system");
        writer.WriteString("content", systemMessage);
        writer.WriteEndObject();

        writer.WriteStartObject();
        writer.WriteString("role", "user");
        writer.WriteString("content", userMessage);
        writer.WriteEndObject();

        writer.WriteEndArray();

        writer.WriteNumber("temperature", settings.Temperature);
        writer.WriteEndObject();
        writer.Flush();

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static IReadOnlyList<string> ParseResponse(string responseBody, int expectedCount) {
        using JsonDocument doc = JsonDocument.Parse(responseBody);
        JsonElement root = doc.RootElement;

        if (!root.TryGetProperty("choices", out JsonElement choices) || choices.GetArrayLength() == 0) {
            throw new InvalidOperationException("No choices in response.");
        }

        JsonElement firstChoice = choices[0];

        if (firstChoice.TryGetProperty("finish_reason", out JsonElement finishReason)) {
            string? reason = finishReason.GetString();
            if (reason == "content_filter") {
                throw new InvalidOperationException("Response was filtered by content policy.");
            }
        }

        if (!firstChoice.TryGetProperty("message", out JsonElement message) ||
            !message.TryGetProperty("content", out JsonElement contentElement)) {
            throw new InvalidOperationException("No message content in response.");
        }

        string? content = contentElement.GetString();
        if (content == null) {
            throw new InvalidOperationException("No message content in response.");
        }

        if (string.IsNullOrWhiteSpace(content)) {
            throw new InvalidOperationException("Empty response content.");
        }

        string fixedJson = FixJsonResponse(content);
        List<string>? translations = JsonSerializer.Deserialize<List<string>>(fixedJson);
        if (translations == null) {
            throw new InvalidOperationException("Failed to parse translations from response.");
        }

        if (translations.Count != expectedCount) {
            throw new InvalidOperationException($"Expected {expectedCount} translation(s) but received {translations.Count}.");
        }

        return translations;
    }

    private static string FixJsonResponse(string json) {
        string result = json.Trim();

        if (result.StartsWith("```")) {
            int firstNewline = result.IndexOf('\n');
            if (firstNewline > 0) {
                result = result.Substring(firstNewline + 1);
            }

            if (result.EndsWith("```")) {
                result = result.Substring(0, result.Length - 3);
            }

            result = result.Trim();
        }

        if (!result.StartsWith("[")) {
            int firstBracket = result.IndexOf('[');
            if (firstBracket >= 0) {
                result = result.Substring(firstBracket);
            }
        }

        if (!result.EndsWith("]")) {
            int lastBracket = result.LastIndexOf(']');
            if (lastBracket >= 0) {
                result = result.Substring(0, lastBracket + 1);
            }
        }

        return result;
    }

    private static HttpClient CreateHttpClient(AzureTranslationSettings settings) {
        HttpClient client = new() {
            BaseAddress = new Uri(settings.Endpoint, UriKind.Absolute)
        };
        client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Add("api-key", settings.ApiKey);
        return client;
    }
}