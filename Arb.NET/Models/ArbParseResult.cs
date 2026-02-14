namespace Arb.NET;

public class ArbParseResult {
    public ArbValidationResults ValidationResults { get; set; } = ArbValidationResults.Valid;
    public ArbDocument? Document { get; set; }

    public override string ToString() {
        if (ValidationResults.IsValid) {
            return $"Valid ARB document with locale '{Document?.Locale ?? "unknown"}' and {Document?.Entries.Count ?? 0} entries.";
        }
        return $"Invalid ARB document with {ValidationResults.Errors.Count} validation errors.\nErrors:\n" + string.Join("\n", ValidationResults.Errors.Select(e => $"- {e}"));
    }
}

public class ArbValidationResults {
    public static readonly ArbValidationResults Valid = new() {
        IsValid = true
    };

    public bool IsValid { get; set; }

    /// <summary>Validation errors. Empty when <see cref="IsValid"/> is true.</summary>
    public IReadOnlyList<ArbValidationError> Errors { get; set; } = Array.Empty<ArbValidationError>();

#if !ARB_GENERATOR
    /// <summary>Creates a failed result from a JsonSchema.Net EvaluationResults.</summary>
    internal static ArbValidationResults FromEvaluationResults(Json.Schema.EvaluationResults results) {
        var errors = new List<ArbValidationError>();
        CollectErrors(results, errors);
        return new ArbValidationResults {
            IsValid = false,
            Errors = errors
        };
    }

    private static void CollectErrors(Json.Schema.EvaluationResults node, List<ArbValidationError> errors) {
        if (node.Errors != null)
            foreach (var kvp in node.Errors)
                errors.Add(new ArbValidationError(kvp.Key, kvp.Value, node.InstanceLocation.ToString(), node.EvaluationPath.ToString()));
        if (node.Details != null)
            foreach (var child in node.Details)
                CollectErrors(child, errors);
    }
#endif
}

/// <summary>A single validation error from schema evaluation.</summary>
public record ArbValidationError {
    /// <summary>The JSON Schema keyword that triggered the error (e.g. "type", "required", "enum").</summary>
    public string Keyword { get; }

    /// <summary>Human-readable description of what was wrong.</summary>
    public string Message { get; }

    /// <summary>JSON Pointer to the location in the document where the error occurred (e.g. "/@welcomeMessage/placeholders/count").</summary>
    public string InstanceLocation { get; }

    /// <summary>JSON Pointer into the schema that triggered the error.</summary>
    public string SchemaPath { get; }

    internal ArbValidationError(string keyword, string message, string instanceLocation, string schemaPath) {
        Keyword = keyword;
        Message = message;
        InstanceLocation = instanceLocation;
        SchemaPath = schemaPath;
    }

    public override string ToString() => $"[{Keyword}] at '{InstanceLocation}': {Message}";
}