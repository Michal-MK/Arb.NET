using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Arb.NET.IDE.Common.Models;
using Arb.NET.IDE.Common.Services;
using Arb.NET.IDE.VisualStudio.Tool.Models;

namespace Arb.NET.IDE.VisualStudio.Tool.Services;

// ReSharper disable once InconsistentNaming
internal class AzureOpenAITranslationService(AzureTranslationSettings settings, TranslationProvider provider = TranslationProvider.AzureOpenAI) {
    private const int DEFAULT_BATCH_SIZE = 20;

    private readonly ITranslator translator = provider == TranslationProvider.Google
        ? new GoogleTranslator()
        : new AzureOpenAITranslator(settings);

    public (bool Valid, string Error) ValidateSettings() {
        (bool valid, string? error) = translator.ValidateSettings();
        return (valid, error ?? string.Empty);
    }

    public async Task TranslateAsync(
        string sourceLocale,
        string targetLocale,
        List<TranslationItem> items,
        CancellationToken cancellationToken,
        IProgress<(int completed, int total, string message)> progress
    ) {

        if (items.Count == 0) {
            progress.Report((0, 0, $"No items to translate for {targetLocale}."));
            return;
        }

        int completed = 0;
        int total = items.Count;
        List<List<TranslationItem>> batches = CreateBatches(items, DEFAULT_BATCH_SIZE);

        foreach (List<TranslationItem> batch in batches) {
            cancellationToken.ThrowIfCancellationRequested();

            progress?.Report((completed, total, $"Translating to {targetLocale}..."));

            IReadOnlyList<string> translations = await translator.TranslateBatchAsync(
                sourceLocale,
                targetLocale,
                batch.Select(MapItem).ToList(),
                cancellationToken);

            for (int i = 0; i < batch.Count; i++) {
                batch[i].ProposedTranslation = translations[i];
            }

            completed += batch.Count;
            progress?.Report((completed, total, $"Translated {completed}/{total} items for {targetLocale}"));
        }
    }

    private static AzureTranslationItem MapItem(TranslationItem item) {
        return new AzureTranslationItem {
            Key = item.Key,
            SourceText = item.SourceText,
            Description = item.Description
        };
    }

    private static List<List<T>> CreateBatches<T>(List<T> items, int batchSize) {
        List<List<T>> batches = [];
        for (int i = 0; i < items.Count; i += batchSize) {
            batches.Add(items.GetRange(i, Math.Min(batchSize, items.Count - i)));
        }
        return batches;
    }
}