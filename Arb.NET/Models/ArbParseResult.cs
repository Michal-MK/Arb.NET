#if !ARB_GENERATOR
using Json.Schema;
#endif

namespace Arb.NET;

public class ArbParseResult {
    public ArbValidationResults ValidationResults { get; set; } = ArbValidationResults.VALID;
    public ArbDocument? Document { get; set; }

    public override string ToString() {
        if (ValidationResults.IsValid) {
            return $"Valid ARB document with locale '{Document?.Locale ?? "unknown"}' and {Document?.Entries.Count ?? 0} entries.";
        }
        return $"Invalid ARB document with {ValidationResults.Errors.Count} validation errors.\nErrors:\n" + string.Join("\n", ValidationResults.Errors.Select(e => $"- {e}"));
    }
}

public class ArbValidationResults {
    public static readonly ArbValidationResults VALID = new() {
        IsValid = true
    };

    public bool IsValid { get; set; }

    /// <summary>Validation errors. Empty when <see cref="IsValid"/> is true.</summary>
    public IReadOnlyList<ArbValidationError> Errors { get; private set; } = [];

#if !ARB_GENERATOR
    /// <summary>Creates a failed result from a JsonSchema.Net EvaluationResults.</summary>
    internal static ArbValidationResults FromEvaluationResults(EvaluationResults results) {
        List<ArbValidationError> errors = [];
        CollectErrors(results, errors);
        return new ArbValidationResults {
            IsValid = false,
            Errors = errors
        };
    }

    private static void CollectErrors(EvaluationResults node, List<ArbValidationError> errors) {
        if (node.Errors != null) {
            foreach (KeyValuePair<string, string> kvp in node.Errors) {
                errors.Add(new ArbValidationError(kvp.Key, kvp.Value, node.InstanceLocation.ToString(), node.EvaluationPath.ToString()));
            }
        }
        if (node.Details != null) {
            foreach (EvaluationResults? child in node.Details) {
                CollectErrors(child, errors);
            }
        }
    }
#endif
}

public record ArbValidationError(string Keyword, string Message, string InstanceLocation, string SchemaPath) {

    /// <summary>The JSON Schema keyword that triggered the error (e.g. "type", "required", "enum").</summary>
    public string Keyword { get; } = Keyword;

    /// <summary>Human-readable description of what was wrong.</summary>
    public string Message { get; } = Message;

    /// <summary>JSON Pointer to the location in the document where the error occurred (e.g. "/@text/placeholders/count").</summary>
    public string InstanceLocation { get; } = InstanceLocation;

    /// <summary>JSON Pointer into the schema that triggered the error.</summary>
    public string SchemaPath { get; } = SchemaPath;

    public override string ToString() => $"[{Keyword}] at '{InstanceLocation}': {Message}";
}