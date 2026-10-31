namespace EasyRedmineTool.Core.Models.TimeEntries;

using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class JsonValueAsStringConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number or JsonTokenType.StartObject or JsonTokenType.StartArray => ReadStructuredValue(ref reader),
            JsonTokenType.True => "true",
            JsonTokenType.False => "false",
            JsonTokenType.Null => null,
            _ => throw new JsonException($"Unexpected JSON token {reader.TokenType} for a custom field value.")
        };
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value);
    }

    private static string ReadStructuredValue(ref Utf8JsonReader reader)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        return document.RootElement.GetRawText();
    }
}
