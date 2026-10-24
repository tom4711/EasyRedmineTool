namespace EasyRedmineTool.Core.Configuration;

public class TimeEntryCustomFieldActivityRule
{
    public string ActivityName { get; set; } = string.Empty;

    public int? ActivityId { get; set; }

    public List<TimeEntryCustomFieldActivityRuleField> Fields { get; set; } = [];
}

public class TimeEntryCustomFieldActivityRuleField
{
    public string Name { get; set; } = string.Empty;

    public int Id { get; set; }

    public string DefaultValue { get; set; } = string.Empty;

    public List<string> Values { get; set; } = [];

    public bool IsRequired { get; set; } = true;

    public bool IsMultiple { get; set; }

    public List<string> PossibleValues { get; set; } = [];

    public string PossibleValuesFile { get; set; } = string.Empty;
}
