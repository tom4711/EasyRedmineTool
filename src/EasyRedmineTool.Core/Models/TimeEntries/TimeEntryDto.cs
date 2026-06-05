namespace EasyRedmineTool.Core.Models.TimeEntries
{
    public class TimeEntryDto
    {
        public int Id { get; set; }
        public int Issue_Id { get; set; }
        public double Hours { get; set; }
        public string Spent_On { get; set; } = string.Empty;
        public int Activity_Id { get; set; }
        public string Comments { get; set; } = string.Empty;
    }
}
