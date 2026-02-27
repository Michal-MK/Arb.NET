namespace Arb.NET.IDE.VisualStudio.Tool.CodeCompletion.Models;

internal sealed class ArbCompletionItem(string key, bool isParametric, string? description, string? arbFilePath, string? rawKey) {

    public string Key { get; } = key;
    public bool IsParametric { get; } = isParametric;
    public string? Description { get; } = description;
    public string? ArbFilePath { get; } = arbFilePath;
    public string? RawKey { get; } = rawKey;
}