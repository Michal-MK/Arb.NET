using System.Text.Json.Serialization;
using Arb.NET.Models;
using Arb.NET.Translation.Models;

namespace Arb.NET.Wasm;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ArbParseResult))]
[JsonSerializable(typeof(ArbDocument))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(CsvImportPreview))]
[JsonSerializable(typeof(CsvImportApplyResult))]
[JsonSerializable(typeof(CsvApplyImportResponse))]
[JsonSerializable(typeof(AzureTranslationSettings))]
[JsonSerializable(typeof(AzureTranslationItem))]
[JsonSerializable(typeof(List<AzureTranslationItem>))]
[JsonSerializable(typeof(IReadOnlyList<string>))]
[JsonSerializable(typeof(ValidateSettingsResponse))]
internal partial class ArbJsonContext : JsonSerializerContext;
