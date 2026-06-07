namespace EasyRedmineTool.Core.Models.TimeEntries;

public class TimeEntryIssueDto
{
    public int Id { get; set; }

    public string Subject { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string GetDisplaySubject()
    {
        if (!string.IsNullOrWhiteSpace(Subject))
        {
            return Subject;
        }

        if (!string.IsNullOrWhiteSpace(Name))
        {
            return Name;
        }

        return string.Empty;
    }
}
