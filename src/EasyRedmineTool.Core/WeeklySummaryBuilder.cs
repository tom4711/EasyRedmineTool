namespace EasyRedmineTool.Core;

using EasyRedmineTool.Core.Models.TimeEntries;
using EasyRedmineTool.Core.ViewModels;

using System.Globalization;

public static class WeeklySummaryBuilder
{
    public static IReadOnlyList<WeeklyHoursRowViewModel> Build(
        IEnumerable<TimeEntryDto> entries,
        IReadOnlyDictionary<int, string> ticketSubjectsById)
    {
        return entries
            .Select(entry => new { Entry = entry, Date = RedmineDates.TryParseSpentOn(entry.Spent_On) })
            .Where(x => x.Date.HasValue)
            .GroupBy(x => new
            {
                Year = ISOWeek.GetYear(x.Date!.Value),
                Week = ISOWeek.GetWeekOfYear(x.Date!.Value)
            })
            .OrderBy(g => g.Key.Year)
            .ThenBy(g => g.Key.Week)
            .Select(g =>
            {
                var tickets = g
                    .GroupBy(x => x.Entry.GetIssueId())
                    .Select(ticketGroup => new WeeklyTicketHoursDto
                    {
                        IssueId = ticketGroup.Key,
                        Subject = ResolveSubject(ticketGroup.Key, ticketGroup.First().Entry, ticketSubjectsById),
                        Hours = Math.Round(ticketGroup.Sum(x => x.Entry.Hours), 2)
                    })
                    .OrderByDescending(ticket => ticket.Hours)
                    .ThenBy(ticket => ticket.IssueId)
                    .ToList();

                return new WeeklyHoursRowViewModel(
                    g.Key.Year,
                    g.Key.Week,
                    Math.Round(g.Sum(x => x.Entry.Hours), 2),
                    tickets);
            })
            .ToList();
    }

    internal static string ResolveSubject(
        int issueId,
        TimeEntryDto entry,
        IReadOnlyDictionary<int, string> ticketSubjectsById)
    {
        var fromEntry = entry.Issue?.GetDisplaySubject();
        if (!string.IsNullOrWhiteSpace(fromEntry))
        {
            return fromEntry;
        }

        if (ticketSubjectsById.TryGetValue(issueId, out var cachedSubject)
            && !string.IsNullOrWhiteSpace(cachedSubject))
        {
            return cachedSubject;
        }

        return "Unbekanntes Ticket";
    }
}
