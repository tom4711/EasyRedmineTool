namespace EasyRedmineTool.Core.Models.TimeEntries;

public class TimeEntryCustomFieldDefinitionDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string FieldFormat { get; set; } = "string";

    public bool IsRequired { get; set; }

    public bool IsForAll { get; set; }

    public bool IsProjectScoped { get; set; }

    public IReadOnlyList<int> ProjectIds { get; set; } = [];

    public IReadOnlyList<int> ActivityIds { get; set; } = [];

    public IReadOnlyList<string> PossibleValues { get; set; } = [];

    public bool Multiple { get; set; }

    public bool HasPossibleValues =>
        PossibleValues.Count > 0 && IsListFormat();

    public bool IsSearchableList => HasPossibleValues && PossibleValues.Count > 12;

    public bool IsListFormat() =>
        FieldFormat.Equals("list", StringComparison.OrdinalIgnoreCase)
        || FieldFormat.Equals("enumeration", StringComparison.OrdinalIgnoreCase)
        || FieldFormat.Equals("depending_enumeration", StringComparison.OrdinalIgnoreCase);
}
