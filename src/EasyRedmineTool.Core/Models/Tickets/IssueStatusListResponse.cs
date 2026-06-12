namespace EasyRedmineTool.Core.Models.Tickets;

using System.Text.Json.Serialization;

public class IssueStatusListResponse
{
    [JsonPropertyName("issue_statuses")]
    public List<StatusDto> Issue_Statuses { get; set; } = [];
}
