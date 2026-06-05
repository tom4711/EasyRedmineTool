namespace EasyRedmineTool.Core.Models.TimeEntries;

using System.Text.Json.Serialization;

public class TimeEntriesListResponse
{
    [JsonPropertyName("time_entries")]
    public List<TimeEntryDto> Time_Entries { get; set; } = [];

    [JsonPropertyName("total_count")]
    public int Total_Count { get; set; }

    public int Offset { get; set; }
    public int Limit { get; set; }
}
