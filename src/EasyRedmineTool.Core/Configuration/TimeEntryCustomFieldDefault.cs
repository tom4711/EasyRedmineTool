namespace EasyRedmineTool.Core.Configuration;

public class TimeEntryCustomFieldDefault
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public List<string> Values { get; set; } = [];
}
