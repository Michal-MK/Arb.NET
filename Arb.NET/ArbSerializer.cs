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
        JsonWriterOptions options = new() { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        using (Utf8JsonWriter writer = new(buffer, options)) {
            writer.WriteStartObject();

            if (!string.IsNullOrEmpty(doc.Locale))
                writer.WriteString("@@locale", doc.Locale);

            if (!string.IsNullOrEmpty(doc.Context))
                writer.WriteString("@@context", doc.Context);

            foreach (var kvp in doc.Entries.OrderBy(e => e.Key)) {
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
        writer.WriteStartObject("@" + key);

        if (metadata.Description != null)
            writer.WriteString("description", metadata.Description);

        if (metadata.Placeholders is { Count: > 0 }) {
            writer.WriteStartObject("placeholders");
            foreach (var kvp in metadata.Placeholders) {
                string name = kvp.Key;
                ArbPlaceholder placeholder = kvp.Value;
                writer.WriteStartObject(name);
                writer.WriteString("type", placeholder.Type);
                writer.WriteEndObject();
            }
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }
}