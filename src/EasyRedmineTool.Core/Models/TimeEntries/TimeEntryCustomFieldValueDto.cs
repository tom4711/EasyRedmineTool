namespace EasyRedmineTool.Core.Models.TimeEntries;

using System.Text.Json.Serialization;

public class TimeEntryCustomFieldValueDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonValueAsStringConverter))]
    public string? Value { get; set; }
}
