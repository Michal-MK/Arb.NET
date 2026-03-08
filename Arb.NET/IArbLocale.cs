namespace Arb.NET;

/// <summary>
/// Provides locale-aware string lookup by raw ARB key.
/// Implemented by the generated dispatcher class.
/// </summary>
public interface IArbLocale {
    string? this[string? key] { get; }
}
