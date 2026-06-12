namespace EasyRedmineTool.Core;

using EasyRedmineTool.Core.Models.SeriesBooking;
using EasyRedmineTool.Core.Models.TimeEntries;

public static class SeriesBookingPlanner
{
    public static IReadOnlyList<DateTime> GenerateDates(
        DateTime from,
        DateTime to,
        IReadOnlySet<DayOfWeek> includedWeekdays)
    {
        if (to.Date < from.Date || includedWeekdays.Count == 0)
        {
            return [];
        }

        var dates = new List<DateTime>();
        for (var date = from.Date; date <= to.Date; date = date.AddDays(1))
        {
            if (!includedWeekdays.Contains(date.DayOfWeek))
            {
                continue;
            }

            dates.Add(date);
        }

        return dates;
    }

    public static IReadOnlyList<DateTime> GenerateDates(DateTime from, DateTime to, SeriesBookingDayMode dayMode) =>
        dayMode switch
        {
            SeriesBookingDayMode.WeekdaysOnly => GenerateDates(from, to, SeriesBookingWeekdays.Weekdays),
            SeriesBookingDayMode.AllDays => GenerateDates(from, to, SeriesBookingWeekdays.All),
            _ => []
        };

    public static bool IsWeekend(DateTime date) =>
        date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    public static HashSet<DateTime> FindConflictDates(int issueId, IReadOnlyList<TimeEntryDto> existingEntries)
    {
        var conflicts = new HashSet<DateTime>();

        foreach (var entry in existingEntries)
        {
            if (entry.GetIssueId() != issueId)
            {
                continue;
            }

            var spentOn = RedmineDates.TryParseSpentOn(entry.Spent_On);
            if (spentOn.HasValue)
            {
                conflicts.Add(spentOn.Value.Date);
            }
        }

        return conflicts;
    }
}
