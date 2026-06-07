namespace EasyRedmineTool.Core.Models.TimeEntries;

using System.Globalization;

public class WeeklyTicketHoursDto
{
    public int IssueId { get; init; }

    public string Subject { get; init; } = string.Empty;

    public double Hours { get; init; }

    public string Label => $"#{IssueId}  {Subject}";

    public string HoursDisplay => FormatHours(Hours);

    internal static string FormatHours(double hours) =>
        $"{hours.ToString("0.##", CultureInfo.GetCultureInfo("de-DE"))} h";
}
