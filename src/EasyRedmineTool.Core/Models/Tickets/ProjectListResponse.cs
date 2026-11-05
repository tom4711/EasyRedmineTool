namespace EasyRedmineTool.Core.Models.Tickets;

public sealed class ProjectListResponse
{
    public List<ProjectDto> Projects { get; set; } = [];
    public int Total_Count { get; set; }
    public int Offset { get; set; }
    public int Limit { get; set; }
}
