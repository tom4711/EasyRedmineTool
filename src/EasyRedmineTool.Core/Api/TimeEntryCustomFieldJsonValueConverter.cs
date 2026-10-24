namespace EasyRedmineTool.Core.Api;

using System.Text.Json;
using System.Text.Json.Serialization;

internal sealed class TimeEntryCustomFieldJsonValueConverter : JsonConverter<List<string>>
{
    public override List<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            return string.IsNullOrWhiteSpace(value) ? [] : [value.Trim()];
        }

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var values = new List<string>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType == JsonTokenType.String)
                {
                    var value = reader.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        values.Add(value.Trim());
                    }
                }
            }

            return values;
        }

        if (reader.TokenType == JsonTokenType.Null)
        {
            return [];
        }

        reader.Skip();
        return [];
    }

    public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options)
    {
        if (value.Count <= 1)
        {
            writer.WriteStringValue(value.FirstOrDefault() ?? string.Empty);
            return;
        }

        writer.WriteStartArray();
        foreach (var item in value)
        {
            writer.WriteStringValue(item);
        }

        writer.WriteEndArray();
    }
}
