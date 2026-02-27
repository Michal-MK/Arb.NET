using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Arb.NET.IDE.VisualStudio.Tool.CodeCompletion.Models;
using Arb.NET.Models;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace Arb.NET.IDE.VisualStudio.Tool.CodeCompletion;

[Export(typeof(ICompletionSourceProvider))]
[ContentType("xaml")]
[Order(After = "default")]
[Name("ArbXamlCompletionProvider")]
public class ArbXamlCompletionSourceProvider : ICompletionSourceProvider {
    public ICompletionSource TryCreateCompletionSource(ITextBuffer textBuffer) {
        return textBuffer.Properties.GetOrCreateSingletonProperty(() => new ArbXamlCompletionSource(textBuffer));
    }
}

public class ArbXamlCompletionSource(ITextBuffer textBuffer) : ICompletionSource {
    //  // TODO(naive) Fails with global implicit xaml namespaces and probably more... also DUPLICATE
    private static readonly Regex ARB_PREFIX_PATTERN = new(@"\{[^:}]+:Arb\s+(\w*)$", RegexOptions.Compiled);

    public void AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets) {
        ITextBuffer activeBuffer = session.TextView.TextDataModel.DocumentBuffer;

        SnapshotPoint? caretPoint = session.TextView.Caret.Position.Point.GetPoint(
            activeBuffer,
            session.TextView.Caret.Position.Affinity
        );

        if (!caretPoint.HasValue) {
            SnapshotPoint? triggerPoint = session.GetTriggerPoint(activeBuffer.CurrentSnapshot);
            if (!triggerPoint.HasValue) return;
            caretPoint = triggerPoint.Value;
        }

        SnapshotPoint point = caretPoint.Value;

        ITextSnapshotLine line = point.GetContainingLine();
        string textBeforeCaret = point.Snapshot.GetText(line.Start.Position, point.Position - line.Start.Position);

        Match match = ARB_PREFIX_PATTERN.Match(textBeforeCaret);
        if (!match.Success) return;

        // TODO(naive) This is naive and may produce false positives.
        // We are inside {ext:Arb ...}: suppress default XAML completion sets.
        completionSets.Clear();

        string typedKey = match.Groups[1].Value;
        string? projectDir = TryFindProjectDirectory(activeBuffer, session.TextView.TextBuffer, textBuffer);
        if (string.IsNullOrWhiteSpace(projectDir)) return;

        List<ArbCompletionItem> keyInfos = ArbKeyIndex.GetKeys(projectDir!);
        if (keyInfos.Count == 0) return;

        List<Completion> completions = keyInfos
            .Where(it => it.Key.StartsWith(typedKey, StringComparison.OrdinalIgnoreCase))
            .Select(it => new Completion(
                        it.Key,
                        it.Key,
                        BuildDescription(it),
                        null,
                        null
                    ))
            .ToList();

        if (completions.Count == 0) return;

        Group keyGroup = match.Groups[1];
        int replacementStart = line.Start.Position + keyGroup.Index;
        int replacementLength = keyGroup.Length;
        ITrackingSpan applicableTo = point.Snapshot.CreateTrackingSpan(
            new Span(replacementStart, replacementLength), SpanTrackingMode.EdgeInclusive
        );

        completionSets.Add(
            new CompletionSet(
                "ArbXaml",
                "ARB Keys",
                applicableTo,
                completions,
                null
            )
        );
    }

    private static string? TryFindProjectDirectory(params ITextBuffer[] buffers) {
        foreach (ITextBuffer buffer in buffers.Where(b => b != null)) {
            if (!buffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document)) continue;

            string? projectDir = ArbKeyIndex.FindProjectDirFromFilePath(document.FilePath);
            if (!string.IsNullOrWhiteSpace(projectDir)) {
                return projectDir;
            }
        }

        return null;
    }

    private static string BuildDescription(ArbCompletionItem item) {
        List<string> parts = [];
        if (item.IsParametric) {
            parts.Add("⚠ parametric");
        }

        if (!string.IsNullOrWhiteSpace(item.Description)) {
            parts.Add(item.Description!);
        }

        return parts.Count == 0 ? "ARB key" : "ARB key — " + string.Join(" | ", parts);
    }

    public void Dispose() { }
}

internal static class ArbKeyIndex {
    // TODO(naive) assumes generator stability
    private static readonly Regex PUBLIC_PROPERTY_REGEX =
        new(@"^\s+public\s+string\s+(\w+)\s*=>", RegexOptions.Multiline | RegexOptions.Compiled);

    // TODO(naive) assumes generator stability
    private static readonly Regex PUBLIC_METHOD_REGEX =
        new(@"^\s+public\s+string\s+(\w+)\s*\(", RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly ConcurrentDictionary<string, CacheEntry> CACHE = new(StringComparer.OrdinalIgnoreCase);

    public static string? FindProjectDirFromFilePath(string? filePath) {
        if (string.IsNullOrWhiteSpace(filePath)) return null;

        string? currentDirectory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(currentDirectory)) return null;

        DirectoryInfo? dir = new(currentDirectory);
        while (dir != null) {
            // TODO(l10n) This is naive, the l10n.yaml may exist elsewhere (e.g. a directory with the arb files)
            if (File.Exists(Path.Combine(dir.FullName, "l10n.yaml"))) {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }

    public static List<ArbCompletionItem> GetKeys(string projectDir) {
        // TODO(l10n) This is naive, the l10n.yaml may exist elsewhere (e.g. a directory with the arb files)
        string localizationPath = Path.Combine(projectDir, "l10n.yaml");
        if (!File.Exists(localizationPath)) {
            return [];
        }

        L10nConfig config;
        try {
            config = L10nConfig.Parse(File.ReadAllText(localizationPath));
        }
        catch {
            return [];
        }
        
        // TODO(cleanup) Output class should be a required config value. No need to fallback here.
        string outputClass = string.IsNullOrWhiteSpace(config.OutputClass) ? "AppLocale" : config.OutputClass!;
        string arbPath = string.IsNullOrWhiteSpace(config.TemplateArbFile)
            ? string.Empty
            : Path.Combine(projectDir, config.ArbDir, config.TemplateArbFile);

        DateTime localizationStamp = SafeLastWriteUtc(localizationPath);
        DateTime arbStamp = string.IsNullOrWhiteSpace(arbPath) ? DateTime.MinValue : SafeLastWriteUtc(arbPath);

        if (CACHE.TryGetValue(projectDir, out CacheEntry cached)
            && cached.LocalizationStamp == localizationStamp
            && cached.ArbStamp == arbStamp) {
            return cached.Keys;
        }

        List<ArbCompletionItem> keys = TryGetFromGeneratedFile(projectDir, outputClass, arbPath);
        if (keys.Count == 0) {
            keys = GetFromArbFile(arbPath);
        }

        CACHE[projectDir] = new CacheEntry(localizationStamp, arbStamp, keys);
        return keys;
    }

    private static List<ArbCompletionItem> TryGetFromGeneratedFile(string projectDir, string outputClass, string arbPath) {
        Dictionary<string, ArbMetadataSnapshot> metadataByPascal = new(StringComparer.Ordinal);

        if (File.Exists(arbPath)) {
            try {
                ArbParseResult parseResult = new ArbParser().Parse(arbPath);
                if (parseResult.Document != null) {
                    foreach (KeyValuePair<string, ArbEntry> kvp in parseResult.Document.Entries) {
                        string pascal = StringHelper.ToPascalCase(kvp.Key);
                        metadataByPascal[pascal] = new ArbMetadataSnapshot(kvp.Key, kvp.Value.Metadata?.Description);
                    }
                }
            }
            catch {
                // Completion should still work without descriptions.
            }
        }

        IEnumerable<string> candidates;
        try {
            // TODO(naive) This assumes the generated file is named exactly {OutputClass}Dispatcher.g.cs and is located somewhere under the project directory.
            // This is true for the default config, but may not be true in general.
            // We should ideally get this info from the build system instead of guessing.
            candidates = Directory.EnumerateFiles(projectDir, outputClass + "Dispatcher.g.cs", SearchOption.AllDirectories);
        }
        catch {
            return [];
        }

        foreach (string generatedPath in candidates) {
            try {
                string content = File.ReadAllText(generatedPath);
                List<ArbCompletionItem> result = [];

                foreach (Match match in PUBLIC_PROPERTY_REGEX.Matches(content)) {
                    string name = match.Groups[1].Value;
                    // TODO(naive) This assumes names, needs a single source of truth
                    if (name is "ResolveLocale" or "TryParent") continue;
                    metadataByPascal.TryGetValue(name, out ArbMetadataSnapshot? metadata);
                    result.Add(new ArbCompletionItem(name, false, metadata?.Description, File.Exists(arbPath) ? arbPath : null, metadata?.RawKey));
                }

                foreach (Match match in PUBLIC_METHOD_REGEX.Matches(content)) {
                    string name = match.Groups[1].Value;
                    if (name == outputClass) continue;
                    // TODO(naive) This assumes names, needs a single source of truth
                    if (name is "ResolveLocale" or "TryParent") continue;
                    metadataByPascal.TryGetValue(name, out ArbMetadataSnapshot? metadata);
                    result.Add(new ArbCompletionItem(name, true, metadata?.Description, File.Exists(arbPath) ? arbPath : null, metadata?.RawKey));
                }

                if (result.Count > 0) {
                    return result
                        .GroupBy(x => x.Key, StringComparer.Ordinal)
                        .Select(g => g.First())
                        .OrderBy(x => x.Key, StringComparer.Ordinal)
                        .ToList();
                }
            }
            catch {
                // Ignore this candidate and try next.
            }
        }

        return [];
    }

    private static List<ArbCompletionItem> GetFromArbFile(string arbPath) {
        if (!File.Exists(arbPath)) {
            return [];
        }

        try {
            ArbParseResult parseResult = new ArbParser().Parse(arbPath);
            if (parseResult.Document == null) {
                return [];
            }

            return parseResult.Document.Entries
                .Select(kvp => {
                    bool isParametric = kvp.Value.IsParametric(out _);
                    string key = StringHelper.ToPascalCase(kvp.Key);
                    string? description = kvp.Value.Metadata?.Description;
                    return new ArbCompletionItem(key, isParametric, description, arbPath, kvp.Key);
                })
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .ToList();
        }
        catch {
            return [];
        }
    }

    // TODO(helpers)
    private static DateTime SafeLastWriteUtc(string path) {
        try {
            return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
        }
        catch {
            return DateTime.MinValue;
        }
    }

    private sealed class CacheEntry(DateTime localizationStamp, DateTime arbStamp, List<ArbCompletionItem> keys) {

        public DateTime LocalizationStamp { get; } = localizationStamp;
        public DateTime ArbStamp { get; } = arbStamp;
        public List<ArbCompletionItem> Keys { get; } = keys;
    }

    private sealed class ArbMetadataSnapshot(string rawKey, string? description) {

        public string RawKey { get; } = rawKey;
        public string? Description { get; } = description;
    }
}