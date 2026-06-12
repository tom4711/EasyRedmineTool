namespace EasyRedmineTool.Core.Models.Tickets;

using System.Text.Json.Serialization;

public class IssueDto
{
    public int Id { get; set; }
    public string Subject { get; set; } = string.Empty;
    public NamedEntityDto? Project { get; set; }
    public StatusDto? Status { get; set; }
    public NamedEntityDto? Priority { get; set; }
    public NamedEntityDto? Tracker { get; set; }

    [JsonPropertyName("assigned_to")]
    public NamedEntityDto? Assigned_To { get; set; }

    [JsonPropertyName("due_date")]
    public string? Due_Date { get; set; }

    public DateTime? LastTimeEntryOn { get; set; }
}
