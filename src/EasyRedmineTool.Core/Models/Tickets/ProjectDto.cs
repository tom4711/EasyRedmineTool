namespace EasyRedmineTool.Core.Models.Tickets;

using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class ProjectDto : NamedEntityDto
{
    public int Status { get; set; }

    public string Identifier { get; set; } = string.Empty;

    public bool Is_Public { get; set; }

    public NamedEntityDto? Parent { get; set; }

    [JsonPropertyName("custom_fields")]
    public List<ProjectCustomFieldValueDto> Custom_Fields { get; set; } = [];
}

public sealed class ProjectCustomFieldValueDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public JsonElement Value { get; set; }

    public string GetDisplayValue()
    {
        return Value.ValueKind switch
        {
            JsonValueKind.True => "ja",
            JsonValueKind.False => "nein",
            JsonValueKind.String => Value.GetString()?.Trim() ?? string.Empty,
            JsonValueKind.Number => Value.ToString(),
            JsonValueKind.Array => string.Join(", ", Value.EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString())
                .Where(item => !string.IsNullOrWhiteSpace(item))),
            _ => string.Empty
        };
    }

}
