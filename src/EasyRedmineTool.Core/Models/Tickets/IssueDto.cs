namespace EasyRedmineTool.Core.Models.Tickets;

using System.Text.Json.Serialization;

public class IssueDto
{
    public int Id { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string? Description { get; set; }
    public NamedEntityDto? Project { get; set; }
    public StatusDto? Status { get; set; }
    public NamedEntityDto? Priority { get; set; }
    public NamedEntityDto? Tracker { get; set; }

    public NamedEntityDto? Author { get; set; }

    public NamedEntityDto? Category { get; set; }

    [JsonPropertyName("fixed_version")]
    public NamedEntityDto? Fixed_Version { get; set; }

    [JsonPropertyName("assigned_to")]
    public NamedEntityDto? Assigned_To { get; set; }

    [JsonPropertyName("due_date")]
    public string? Due_Date { get; set; }

    [JsonPropertyName("start_date")]
    public string? Start_Date { get; set; }

    [JsonPropertyName("created_on")]
    public DateTime? Created_On { get; set; }

    [JsonPropertyName("updated_on")]
    public DateTime? Updated_On { get; set; }

    public DateTime? LastTimeEntryOn { get; set; }
}
