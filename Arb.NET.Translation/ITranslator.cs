using Arb.NET.Translation.Models;

namespace Arb.NET.Translation;

public interface ITranslator {
    (bool Valid, string? Error) ValidateSettings();

    Task<IReadOnlyList<string>> TranslateBatchAsync(
        string sourceLocale,
        string targetLocale,
        IReadOnlyList<AzureTranslationItem> items,
        CancellationToken cancellationToken
    );
}