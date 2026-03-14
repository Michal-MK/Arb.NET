using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Arb.NET;

/// <summary>
/// Serializes an <see cref="ArbDocument"/> back to the ARB JSON format.
/// </summary>
public static class ArbSerializer {
    public static string Serialize(ArbDocument doc) {
        MemoryStream buffer = new();
        JsonWriterOptions options = new() {
            Indented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        // This has to be a using block to ensure the writer flushes to the buffer
        using (Utf8JsonWriter writer = new(buffer, options)) {
            writer.WriteStartObject();

            if (!string.IsNullOrEmpty(doc.Locale)) {
                writer.WriteString(Constants.KEY_LOCALE, doc.Locale);
            }

            if (!string.IsNullOrEmpty(doc.Context)) {
                writer.WriteString(Constants.KEY_CONTEXT, doc.Context);
            }

            foreach (KeyValuePair<string, ArbEntry> kvp in doc.Entries.OrderBy(e => e.Key)) {
                string key = kvp.Key;
                ArbEntry entry = kvp.Value;
                writer.WriteString(key, entry.Value);

                if (entry.Metadata != null) {
                    WriteMetadata(writer, key, entry.Metadata);
                }
            }

            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void WriteMetadata(Utf8JsonWriter writer, string key, ArbMetadata metadata) {
        writer.WriteStartObject(Constants.ARB_META_PREFIX + key);

        if (metadata.Description != null) {
            writer.WriteString(Constants.ARB_META_CONTENT_DESCRIPTION, metadata.Description);
        }

        if (metadata.Placeholders is { Count: > 0 }) {
            writer.WriteStartObject(Constants.ARB_MET_CONTENT_PLACEHOLDERS);
            foreach (KeyValuePair<string, ArbPlaceholder> kvp in metadata.Placeholders) {
                string name = kvp.Key;
                ArbPlaceholder placeholder = kvp.Value;
                writer.WriteStartObject(name);
                writer.WriteString(Constants.ARB_META_CONTENT_PLACEHOLDER_TYPE, placeholder.Type);
                writer.WriteEndObject();
            }
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }
}