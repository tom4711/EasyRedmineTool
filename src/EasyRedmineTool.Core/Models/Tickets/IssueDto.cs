namespace EasyRedmineTool.Core.Models.Tickets
{
    public class IssueDto
    {
        public int Id { get; set; }
        public string Subject { get; set; } = string.Empty;
        public NamedEntityDto? Project { get; set; }
        public NamedEntityDto? Status { get; set; }
        public NamedEntityDto? Priority { get; set; }
    }
}
