namespace EasyRedmineTool.Core.Models.TimeEntries;

public class TimeEntryCustomFieldValue
{
    public int Id { get; set; }

    public string Value { get; set; } = string.Empty;

    public List<string> Values { get; set; } = [];

    public bool IsMultiple { get; set; }

    public object GetApiValue() =>
        IsMultiple
            ? Values
            : Value;
}
