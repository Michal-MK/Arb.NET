using Arb.NET.IDE.Common.Models;

namespace Arb.NET.IDE.Common.Services;

public interface ITranslator {
    (bool Valid, string? Error) ValidateSettings();

    Task<IReadOnlyList<string>> TranslateBatchAsync(
        string sourceLocale,
        string targetLocale,
        IReadOnlyList<AzureTranslationItem> items,
        CancellationToken cancellationToken
    );
}