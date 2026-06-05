namespace EasyRedmineTool.Core.Models.TimeEntries;

public class WeeklyHoursDto
{
    public int Year { get; set; }
    public int Week { get; set; }
    public double Hours { get; set; }

    public string Label => $"KW {Week} / {Year}";
}
