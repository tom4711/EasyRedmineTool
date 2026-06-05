namespace EasyRedmineTool.Core.Models.Tickets
{
    using System.Collections.Generic;

    public class IssueListResponse
    {
        public List<IssueDto> Issues { get; set; } = [];
        public int Total_Count { get; set; }
        public int Offset { get; set; }
        public int Limit { get; set; }
    }
}

