namespace EasyRedmineTool.Tests;

using EasyRedmineTool.Core;
using EasyRedmineTool.Core.Models.SeriesBooking;
using EasyRedmineTool.Core.Models.TimeEntries;

public class SeriesBookingPlannerTests
{
    [Fact]
    public void GenerateDates_weekdays_only_skips_weekends()
    {
        var from = new DateTime(2025, 6, 9);
        var to = new DateTime(2025, 6, 15);

        var dates = SeriesBookingPlanner.GenerateDates(from, to, SeriesBookingDayMode.WeekdaysOnly);

        Assert.Equal(5, dates.Count);
        Assert.All(dates, date => Assert.False(SeriesBookingPlanner.IsWeekend(date)));
        Assert.Equal(new DateTime(2025, 6, 9), dates[0]);
        Assert.Equal(new DateTime(2025, 6, 13), dates[^1]);
    }

    [Fact]
    public void GenerateDates_all_days_includes_weekends()
    {
        var from = new DateTime(2025, 6, 9);
        var to = new DateTime(2025, 6, 15);

        var dates = SeriesBookingPlanner.GenerateDates(from, to, SeriesBookingDayMode.AllDays);

        Assert.Equal(7, dates.Count);
    }

    [Fact]
    public void GenerateDates_selected_weekdays_filters_individual_days()
    {
        var from = new DateTime(2025, 6, 9);
        var to = new DateTime(2025, 6, 15);
        var onlyTuesdayAndThursday = new HashSet<DayOfWeek>
        {
            DayOfWeek.Tuesday,
            DayOfWeek.Thursday
        };

        var dates = SeriesBookingPlanner.GenerateDates(from, to, onlyTuesdayAndThursday);

        Assert.Equal(2, dates.Count);
        Assert.All(dates, date => Assert.Contains(date.DayOfWeek, onlyTuesdayAndThursday));
    }

    [Fact]
    public void GenerateDates_returns_empty_when_to_before_from()
    {
        var dates = SeriesBookingPlanner.GenerateDates(
            new DateTime(2025, 6, 15),
            new DateTime(2025, 6, 9),
            SeriesBookingDayMode.WeekdaysOnly);

        Assert.Empty(dates);
    }

    [Fact]
    public void FindConflictDates_returns_only_matching_issue_dates()
    {
        var entries = new List<TimeEntryDto>
        {
            new() { Issue_Id = 42, Spent_On = "2025-06-10" },
            new() { Issue_Id = 99, Spent_On = "2025-06-10" },
            new() { Issue_Id = 42, Spent_On = "2025-06-12" },
            new() { Issue_Id = 42, Spent_On = "invalid" }
        };

        var conflicts = SeriesBookingPlanner.FindConflictDates(42, entries);

        Assert.Equal(2, conflicts.Count);
        Assert.Contains(new DateTime(2025, 6, 10), conflicts);
        Assert.Contains(new DateTime(2025, 6, 12), conflicts);
    }
}
