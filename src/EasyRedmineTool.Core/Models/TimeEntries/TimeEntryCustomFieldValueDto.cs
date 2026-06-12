namespace EasyRedmineTool.Core.Models.TimeEntries;

public class TimeEntryCustomFieldValueDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Value { get; set; }
}
