namespace EasyRedmineTool.Core.Models.TimeEntries
{
    public class TimeEntryCreateRequest
    {
        public int IssueId { get; set; }
        public double Hours { get; set; }
        public string SpentOn { get; set; } = string.Empty;
        public int ActivityId { get; set; }
        public string Comments { get; set; } = string.Empty;
    }
}
