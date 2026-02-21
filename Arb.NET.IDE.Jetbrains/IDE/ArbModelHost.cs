using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Arb.NET.IDE.Common.Models;
using Arb.NET.IDE.Common.Services;
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
    private static readonly ILogger LOG = Logger.GetLogger<ArbModelHost>();
    private readonly Dictionary<string, string> localeToFilePath = new();
    private readonly AzureOpenAITranslator translator = new();

    // ReSharper disable once UnusedParameter.Local - not sure is required by some from of reflection-based instantiation.
    public ArbModelHost(ISolution solution, Lifetime _) {
        ArbModel model = solution.GetProtocolSolution().GetArbModel();

        model.GetArbData.SetSync((_, _) => CollectArbData(solution));

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

                parsed.Document.Entries[payload.Key] = new ArbEntry {
                    Key = payload.Key,
                    Value = ""
                };
                File.WriteAllText(filePath, ArbSerializer.Serialize(parsed.Document));
                anyChanged = true;
            }

            return anyChanged;
        });

        model.RemoveArbKey.SetSync((_, payload) => {
            bool anyChanged = false;

            IEnumerable<string> dirFiles = localeToFilePath
                .Where(kv => kv.Key.StartsWith(payload.Directory + "|"))
                .Select(kv => kv.Value);

            foreach (string filePath in dirFiles) {
                string content = File.ReadAllText(filePath);
                ArbParseResult parsed = new ArbParser().ParseContent(content);
                if (parsed.Document == null) continue;
                if (!parsed.Document.Entries.Remove(payload.Key)) continue;

                File.WriteAllText(filePath, ArbSerializer.Serialize(parsed.Document));
                anyChanged = true;
            }

            return anyChanged;
        });

        model.AddArbLocale.SetSync((_, payload) => {
            string newFilePath = Path.Combine(payload.Directory, payload.Locale + ".arb");
            if (File.Exists(newFilePath)) return false;

            // Collect all existing keys from files in this directory.
            IEnumerable<string> dirFiles = localeToFilePath
                .Where(kv => kv.Key.StartsWith(payload.Directory + "|"))
                .Select(kv => kv.Value);

            SortedSet<string> allKeys = new();
            foreach (string filePath in dirFiles) {
                string content = File.ReadAllText(filePath);
                ArbParseResult parsed = new ArbParser().ParseContent(content);
                if (parsed.Document == null) continue;
                foreach (string key in parsed.Document.Entries.Keys) allKeys.Add(key);
            }

            ArbDocument newDoc = new() { Locale = payload.Locale };
            foreach (string key in allKeys) {
                newDoc.Entries[key] = new ArbEntry { Key = key, Value = "" };
            }

            File.WriteAllText(newFilePath, ArbSerializer.Serialize(newDoc));

            string dictKey = payload.Directory + "|" + payload.Locale;
            localeToFilePath[dictKey] = newFilePath;

            return true;
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

        model.TranslateArbEntries.SetAsync(async (_, request) => {
            Arb.NET.IDE.Common.Models.AzureTranslationSettings settings = new() {
                Endpoint = request.Settings.Endpoint,
                DeploymentName = request.Settings.DeploymentName,
                ApiKey = request.Settings.ApiKey,
                CustomPrompt = request.Settings.CustomPrompt,
                Temperature = request.Settings.Temperature
            };

            (bool valid, string? error) = translator.ValidateSettings(settings);
            if (!valid) {
                return new ArbTranslateResponse(false, error, []);
            }

            Dictionary<string, string?> descriptions = LoadDescriptions(request.Directory, request.SourceLocale);

            List<ArbTranslationItem> validItems = request.Items
                .Where(item => !string.IsNullOrWhiteSpace(item.SourceText))
                .Select(item => {
                    string? description = item.Description;
                    if (string.IsNullOrWhiteSpace(description) && descriptions.TryGetValue(item.Key, out string? metadataDescription)) {
                        description = metadataDescription;
                    }

                    return new ArbTranslationItem(item.Key, item.SourceText, description);
                })
                .ToList();

            if (validItems.Count == 0) {
                return new ArbTranslateResponse(true, null, []);
            }

            try {
                IReadOnlyList<string> translated = await translator
                    .TranslateBatchAsync(
                        settings,
                        request.SourceLocale,
                        request.TargetLocale,
                        validItems.Select(item => new AzureTranslationItem {
                            Key = item.Key,
                            SourceText = item.SourceText,
                            Description = item.Description
                        }).ToList(),
                        CancellationToken.None
                    );

                List<ArbTranslatedItem> mapped = validItems
                    .Select((item, index) => new ArbTranslatedItem(item.Key, translated[index]))
                    .ToList();

                return new ArbTranslateResponse(true, null, mapped);
            }
            catch (Exception ex) {
                LOG.Warn($"Azure translation failed: {ex.Message}");
                return new ArbTranslateResponse(false, ex.Message, []);
            }
        });
    }

    private Dictionary<string, string?> LoadDescriptions(string directory, string sourceLocale) {
        Dictionary<string, string?> descriptions = new();
        string dictKey = directory + "|" + sourceLocale;

        if (!localeToFilePath.TryGetValue(dictKey, out string sourceFilePath)) {
            return descriptions;
        }

        try {
            string content = File.ReadAllText(sourceFilePath);
            ArbParseResult parsed = new ArbParser().ParseContent(content);
            if (parsed.Document == null) {
                return descriptions;
            }

            foreach (KeyValuePair<string, ArbEntry> kvp in parsed.Document.Entries) {
                ArbEntry entry = kvp.Value;
                if (!string.IsNullOrWhiteSpace(entry.Metadata?.Description)) {
                    descriptions[kvp.Key] = entry.Metadata?.Description;
                }
            }
        }
        catch (Exception ex) {
            LOG.Warn($"Failed to load ARB descriptions from '{sourceFilePath}': {ex.Message}");
        }

        return descriptions;
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