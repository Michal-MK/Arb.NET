using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arb.NET;
using JetBrains.Application.Parts;
using JetBrains.Lifetimes;
using JetBrains.ProjectModel;
using JetBrains.Rd.Tasks;
using JetBrains.ReSharper.Feature.Services.Protocol;
using JetBrains.Rider.Model;
using JetBrains.Util;
using JetBrains.Util.Logging;

namespace Arb.NET.IDE.Jetbrains.Rider;

[SolutionComponent(Instantiation.ContainerAsyncPrimaryThread)]
public class ArbModelHost {
    private static readonly ILogger Log = Logger.GetLogger<ArbModelHost>(); // [AI-DEBUG]
    private readonly Dictionary<string, string> localeToFilePath = new();

    public ArbModelHost(ISolution solution, Lifetime lifetime) {
        Log.Info("[AI-DEBUG] ArbModelHost constructor: start"); // [AI-DEBUG]
        try {
            ArbModel model = solution.GetProtocolSolution().GetArbModel();
            Log.Info("[AI-DEBUG] ArbModelHost: model obtained"); // [AI-DEBUG]

            model.GetArbData.SetSync((_, _) => {
                Log.Info("[AI-DEBUG] GetArbData: called"); // [AI-DEBUG]
                try {
                    return CollectArbData(solution);
                } catch (Exception ex) {
                    Log.Error(ex, "[AI-DEBUG] GetArbData: unhandled exception"); // [AI-DEBUG]
                    throw;
                }
            });

            model.SaveArbEntry.SetSync((_, update) => {
                Log.Info($"[AI-DEBUG] SaveArbEntry: key={update.Key} locale={update.Locale}"); // [AI-DEBUG]
                try {
                    string dictKey = update.Directory + "|" + update.Locale;
                    if (!localeToFilePath.TryGetValue(dictKey, out string filePath)) {
                        Log.Warn($"[AI-DEBUG] SaveArbEntry: no file found for dictKey={dictKey}"); // [AI-DEBUG]
                        return false;
                    }

                    string content = File.ReadAllText(filePath);
                    ArbParseResult parsed = new ArbParser().ParseContent(content);
                    if (parsed.Document == null) return false;

                    if (!parsed.Document.Entries.TryGetValue(update.Key, out ArbEntry entry)) {
                        return false;
                    }

                    entry.Value = update.Value;
                    File.WriteAllText(filePath, ArbSerializer.Serialize(parsed.Document));
                    return true;
                } catch (Exception ex) {
                    Log.Error(ex, "[AI-DEBUG] SaveArbEntry: unhandled exception"); // [AI-DEBUG]
                    throw;
                }
            });

            model.RenameArbKey.SetSync((_, rename) => {
                Log.Info($"[AI-DEBUG] RenameArbKey: {rename.OldKey} -> {rename.NewKey}"); // [AI-DEBUG]
                try {
                    bool anyChanged = false;

                    // Only rename within files that belong to the same directory.
                    IEnumerable<string> dirFiles = localeToFilePath
                        .Where(kv => kv.Key.StartsWith(rename.Directory + "|"))
                        .Select(kv => kv.Value);

                    foreach (string filePath in dirFiles) {
                        string content = File.ReadAllText(filePath);
                        ArbParseResult parsed = new ArbParser().ParseContent(content);
                        if (parsed.Document == null) continue;

                        ArbDocument doc = parsed.Document;
                        if (!doc.Entries.TryGetValue(rename.OldKey, out ArbEntry entry)) continue;

                        // Rebuild Entries preserving insertion order with the new key name.
                        Dictionary<string, ArbEntry> newEntries = new();
                        foreach (var kvp in doc.Entries) {
                            if (kvp.Key == rename.OldKey) {
                                newEntries[rename.NewKey] = entry with { Key = rename.NewKey };
                            } else {
                                newEntries[kvp.Key] = kvp.Value;
                            }
                        }

                        doc.Entries = newEntries;
                        File.WriteAllText(filePath, ArbSerializer.Serialize(doc));
                        anyChanged = true;
                    }

                    return anyChanged;
                } catch (Exception ex) {
                    Log.Error(ex, "[AI-DEBUG] RenameArbKey: unhandled exception"); // [AI-DEBUG]
                    throw;
                }
            });

            Log.Info("[AI-DEBUG] ArbModelHost constructor: done"); // [AI-DEBUG]
        } catch (Exception ex) {
            Log.Error(ex, "[AI-DEBUG] ArbModelHost constructor: unhandled exception"); // [AI-DEBUG]
            throw;
        }
    }

    private List<ArbLocaleData> CollectArbData(ISolution solution) {
        localeToFilePath.Clear();

        string solutionDir = solution.SolutionFilePath.Directory.FullPath;
        Log.Info($"[AI-DEBUG] CollectArbData: scanning {solutionDir}"); // [AI-DEBUG]
        IEnumerable<string> arbFiles = Directory.EnumerateFiles(solutionDir, "*.arb", SearchOption.AllDirectories);

        List<ArbLocaleData> result = [];
        ArbParser parser = new();

        foreach (string filePath in arbFiles) {
            Log.Info($"[AI-DEBUG] CollectArbData: parsing {filePath}"); // [AI-DEBUG]
            string content = File.ReadAllText(filePath);
            ArbParseResult parseResult = parser.ParseContent(content);

            if (parseResult.Document == null) {
                Log.Warn($"[AI-DEBUG] CollectArbData: parse returned null document for {filePath}"); // [AI-DEBUG]
                continue;
            }

            ArbDocument doc = parseResult.Document;
            string locale = string.IsNullOrEmpty(doc.Locale)
                ? Path.GetFileNameWithoutExtension(filePath)
                : doc.Locale;

            string directory = Path.GetDirectoryName(filePath) ?? solutionDir;
            // Key by directory+locale so the same locale in different folders is tracked separately.
            string dictKey = directory + "|" + locale;
            localeToFilePath[dictKey] = filePath;

            List<JetBrains.Rider.Model.ArbEntry> entries = doc.Entries
                .Select(kvp => new JetBrains.Rider.Model.ArbEntry(kvp.Key, kvp.Value.Value))
                .ToList();

            result.Add(new ArbLocaleData(locale, directory, filePath, entries));
        }

        Log.Info($"[AI-DEBUG] CollectArbData: found {result.Count} locale files"); // [AI-DEBUG]
        return result;
    }
}