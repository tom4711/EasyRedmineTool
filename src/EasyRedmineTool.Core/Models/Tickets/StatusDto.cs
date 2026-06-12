namespace EasyRedmineTool.Core.Models.Tickets;

using System.Text.Json.Serialization;

public class StatusDto : NamedEntityDto
{
    [JsonPropertyName("is_closed")]
    public bool Is_Closed { get; set; }
}
