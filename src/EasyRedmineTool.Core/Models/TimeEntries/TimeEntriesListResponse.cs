namespace EasyRedmineTool.Core.Models.TimeEntries;

public class TimeEntriesListResponse
{
    public List<TimeEntryDto> Time_Entries { get; set; } = [];
    public int Total_Count { get; set; }
    public int Offset { get; set; }
    public int Limit { get; set; }
}
