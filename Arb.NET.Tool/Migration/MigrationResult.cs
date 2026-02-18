namespace Arb.NET.Tool.Migration;

internal sealed class MigrationResult
{
    /// <summary>Files that would be written (populated during a dry run).</summary>
    public List<string> PlannedWrites { get; } = [];

    /// <summary>Files actually written.</summary>
    public List<string> WrittenFiles { get; } = [];

    /// <summary>Non-fatal error messages encountered during migration.</summary>
    public List<string> Errors { get; } = [];

    public bool HasErrors => Errors.Count > 0;
}
