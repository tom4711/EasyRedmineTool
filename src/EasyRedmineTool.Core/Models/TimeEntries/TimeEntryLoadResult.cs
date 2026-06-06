namespace EasyRedmineTool.Core.Models.TimeEntries;

public class TimeEntryLoadResult
{
    public bool Success { get; init; }
    public IReadOnlyList<TimeEntryDto> Entries { get; init; } = [];
    public string Message { get; init; } = string.Empty;
}
