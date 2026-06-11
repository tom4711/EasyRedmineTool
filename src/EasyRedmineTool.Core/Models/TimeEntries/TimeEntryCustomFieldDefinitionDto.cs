namespace EasyRedmineTool.Core.Models.TimeEntries;

using System.Text.Json.Serialization;

public class TimeEntryCustomFieldDefinitionDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("customized_type")]
    public string Customized_Type { get; set; } = string.Empty;

    [JsonPropertyName("field_format")]
    public string Field_Format { get; set; } = string.Empty;

    [JsonPropertyName("is_required")]
    public bool Is_Required { get; set; }

    [JsonPropertyName("default_value")]
    public string? Default_Value { get; set; }

    public List<string> Possible_Values { get; set; } = [];

    public bool HasPossibleValues => Possible_Values.Count > 0;
}
