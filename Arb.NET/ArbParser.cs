using System.Text.Json;
#if !ARB_GENERATOR
using Json.Schema;
#endif

namespace Arb.NET;

/// <summary>
/// Parses .arb (Application Resource Bundle) files
/// </summary>
public class ArbParser {
#if !ARB_GENERATOR
    private static readonly JsonSchema _schema = LoadSchema();

    private static JsonSchema LoadSchema() {
        var assembly = typeof(ArbParser).Assembly;
        var specStream = assembly.GetManifestResourceStream("Arb.NET.Specification.arb_spec.json");
        if (specStream == null) throw new InvalidOperationException("Could not find ARB specification resource.");
        var specString = new StreamReader(specStream).ReadToEnd();
        return JsonSchema.FromText(specString);
    }

    /// <summary>
    /// Parses an .arb file and returns the localization data
    /// </summary>
    public ArbParseResult Parse(string filePath)
        => ParseContent(File.ReadAllText(filePath));
#endif

    /// <summary>
    /// Parses ARB content from a JSON string
    /// </summary>
    public ArbParseResult ParseContent(string content) {
        JsonDocumentOptions parseOptions = new() { AllowTrailingCommas = true };
#if !ARB_GENERATOR
        EvaluationOptions options = new() { OutputFormat = OutputFormat.List };

        JsonSchema schema = _schema;
        using JsonDocument jsonForValidation = JsonDocument.Parse(content, parseOptions);
        EvaluationResults evalResult = schema.Evaluate(jsonForValidation.RootElement, options);

        if (!evalResult.IsValid) {
            return new ArbParseResult {
                ValidationResults = ArbValidationResults.FromEvaluationResults(evalResult)
            };
        }
#endif

        using JsonDocument json = JsonDocument.Parse(content, parseOptions);
        JsonElement root = json.RootElement;
        ArbDocument document = new();

        // First pass: collect metadata keyed by their entry name
        Dictionary<string, JsonElement> metadataMap = new();
        foreach (JsonProperty property in root.EnumerateObject()) {
            if (property.Name.StartsWith("@@")) {
                if (property.Name == "@@locale" && property.Value.ValueKind == JsonValueKind.String)
                    document.Locale = property.Value.GetString() ?? string.Empty;
                if (property.Name == "@@context" && property.Value.ValueKind == JsonValueKind.String)
                    document.Context = property.Value.GetString() ?? string.Empty;
            }
            else if (property.Name.StartsWith("@")) {
                metadataMap[property.Name.Substring(1)] = property.Value;
            }
        }

        // Second pass: collect actual translation entries
        foreach (JsonProperty property in root.EnumerateObject()) {
            if (property.Name.StartsWith("@"))
                continue;

            if (property.Value.ValueKind != JsonValueKind.String)
                continue;

            ArbEntry entry = new() {
                Key = property.Name,
                Value = property.Value.GetString() ?? string.Empty
            };

            if (metadataMap.TryGetValue(property.Name, out JsonElement metaElement)) {
                entry.Metadata = ParseMetadata(metaElement);
            }

            document.Entries[property.Name] = entry;
        }

        return new ArbParseResult {
            ValidationResults = ArbValidationResults.Valid,
            Document = document
        };
    }

    private static ArbMetadata ParseMetadata(JsonElement element) {
        ArbMetadata metadata = new();

        if (element.TryGetProperty("description", out JsonElement desc) && desc.ValueKind == JsonValueKind.String) {
            metadata.Description = desc.GetString();
        }

        if (element.TryGetProperty("placeholders", out JsonElement placeholders) &&
            placeholders.ValueKind == JsonValueKind.Object) {
            metadata.Placeholders = [];
            foreach (JsonProperty ph in placeholders.EnumerateObject()) {
                ArbPlaceholder placeholder = new();
                if (ph.Value.TryGetProperty("type", out JsonElement type) && type.ValueKind == JsonValueKind.String) {
                    placeholder.Type = type.GetString() ?? string.Empty;
                }
                metadata.Placeholders[ph.Name] = placeholder;
            }
        }

        return metadata;
    }
}