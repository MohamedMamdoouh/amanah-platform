using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Amanah.Contracts.Serialization;

// Angular <input type="number"> values are numbers at runtime, so JSON.stringify
// may emit {"key_count":3}. Dictionary<string,string> would otherwise reject that.
public sealed class FlexibleStringDictionaryConverter : JsonConverter<Dictionary<string, string>>
{
    public override Dictionary<string, string> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return [];
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected a JSON object.");
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return values;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected a property name.");
            }

            var key = reader.GetString() ?? string.Empty;
            if (!reader.Read())
            {
                throw new JsonException("Expected a property value.");
            }

            values[key] = ReadScalarAsString(ref reader);
        }

        throw new JsonException("JSON object was not closed.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        Dictionary<string, string> value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var (key, raw) in value)
        {
            writer.WriteString(key, raw);
        }

        writer.WriteEndObject();
    }

    private static string ReadScalarAsString(ref Utf8JsonReader reader) =>
        reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString() ?? string.Empty,
            JsonTokenType.Number when reader.TryGetInt64(out var integer) =>
                integer.ToString(CultureInfo.InvariantCulture),
            JsonTokenType.Number => reader.GetDouble().ToString(CultureInfo.InvariantCulture),
            JsonTokenType.Null => string.Empty,
            _ => throw new JsonException("Expected a string or number value."),
        };
}
