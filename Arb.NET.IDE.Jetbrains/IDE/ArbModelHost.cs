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
    private static readonly ILogger Log = Logger.GetLogger<ArbModelHost>();
    private readonly Dictionary<string, string> localeToFilePath = new();

    public ArbModelHost(ISolution solution, Lifetime lifetime) {
        ArbModel model = solution.GetProtocolSolution().GetArbModel();

        model.GetArbData.SetSync((_, _) => { return CollectArbData(solution); });

        model.SaveArbEntry.SetSync((_, update) => {
            string dictKey = update.Directory + "|" + update.Locale;
            if (!localeToFilePath.TryGetValue(dictKey, out string filePath)) {
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
        });

        model.AddArbKey.SetSync((_, payload) => {
            bool anyChanged = false;

            IEnumerable<string> dirFiles = localeToFilePath
                .Where(kv => kv.Key.StartsWith(payload.Directory + "|"))
                .Select(kv => kv.Value);

            foreach (string filePath in dirFiles) {
                string content = File.ReadAllText(filePath);
                ArbParseResult parsed = new ArbParser().ParseContent(content);
                if (parsed.Document == null) continue;
                if (parsed.Document.Entries.ContainsKey(payload.Key)) continue;

                parsed.Document.Entries[payload.Key] = new ArbEntry { Key = payload.Key, Value = "" };
                File.WriteAllText(filePath, ArbSerializer.Serialize(parsed.Document));
                anyChanged = true;
            }

            return anyChanged;
        });

        model.RenameArbKey.SetSync((_, rename) => {
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
                        newEntries[rename.NewKey] = entry with {
                            Key = rename.NewKey
                        };
                    }
                    else {
                        newEntries[kvp.Key] = kvp.Value;
                    }
                }

                doc.Entries = newEntries;
                File.WriteAllText(filePath, ArbSerializer.Serialize(doc));
                anyChanged = true;
            }

            return anyChanged;
        });
    }

    private List<ArbLocaleData> CollectArbData(ISolution solution) {
        localeToFilePath.Clear();

        string solutionDir = solution.SolutionFilePath.Directory.FullPath;
        IEnumerable<string> arbFiles = Directory.EnumerateFiles(solutionDir, "*.arb", SearchOption.AllDirectories);

        List<ArbLocaleData> result = [];
        ArbParser parser = new();

        foreach (string filePath in arbFiles) {
            string content = File.ReadAllText(filePath);
            ArbParseResult parseResult = parser.ParseContent(content);

            if (parseResult.Document == null) {
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

        return result;
    }
}