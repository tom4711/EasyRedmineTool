namespace EasyRedmineTool.Core.Models.TimeEntries;

using EasyRedmineTool.Core.Api;

using System.Text.Json.Serialization;

public class TimeEntryCustomFieldValueDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool Multiple { get; set; }

    [JsonPropertyName("value")]
    [JsonConverter(typeof(TimeEntryCustomFieldJsonValueConverter))]
    public List<string> SelectedValues { get; set; } = [];

    [JsonIgnore]
    public string? Value
    {
        get => SelectedValues.Count switch
        {
            0 => null,
            1 => SelectedValues[0],
            _ => SelectedValues[0]
        };
        set
        {
            SelectedValues.Clear();
            if (!string.IsNullOrWhiteSpace(value))
            {
                SelectedValues.Add(value.Trim());
            }
        }
    }

    public IReadOnlyList<string> GetEffectiveValues() => SelectedValues;
}
