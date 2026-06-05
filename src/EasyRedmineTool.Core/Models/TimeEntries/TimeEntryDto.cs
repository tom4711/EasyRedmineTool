namespace EasyRedmineTool.Core.Models.TimeEntries;

using EasyRedmineTool.Core.Models.Tickets;

using System.Text.Json.Serialization;

public class TimeEntryDto
{
    public int Id { get; set; }

    [JsonPropertyName("issue_id")]
    public int Issue_Id { get; set; }

    public NamedEntityDto? Issue { get; set; }

    public double Hours { get; set; }

    [JsonPropertyName("spent_on")]
    public string Spent_On { get; set; } = string.Empty;

    [JsonPropertyName("activity_id")]
    public int Activity_Id { get; set; }

    public string Comments { get; set; } = string.Empty;

    public int GetIssueId() => Issue?.Id ?? Issue_Id;
}
