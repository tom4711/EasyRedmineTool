namespace EasyRedmineTool.Tests;

using EasyRedmineTool.Core;
using EasyRedmineTool.Core.Models.TimeEntries;

using System.Globalization;

public class WeeklySummaryBuilderTests
{
    [Fact]
    public void Build_GroupsEntriesByWeekAndTicket()
    {
        var entries = new List<TimeEntryDto>
        {
            CreateEntry(1, 100, "2026-06-02", 2.0),
            CreateEntry(2, 100, "2026-06-03", 1.5),
            CreateEntry(3, 200, "2026-06-04", 3.0),
            CreateEntry(4, 300, "2026-05-26", 4.0)
        };

        var subjects = new Dictionary<int, string>
        {
            [100] = "Erstes Ticket",
            [200] = "Zweites Ticket",
            [300] = "Drittes Ticket"
        };

        var rows = WeeklySummaryBuilder.Build(entries, subjects);

        Assert.Equal(2, rows.Count);

        var week23 = rows.Single(row => row.Week == ISOWeek.GetWeekOfYear(new DateTime(2026, 6, 2)));
        Assert.Equal(6.5, week23.Hours);
        Assert.Equal(2, week23.Tickets.Count);
        Assert.Equal(100, week23.Tickets[0].IssueId);
        Assert.Equal(3.5, week23.Tickets[0].Hours);
        Assert.Equal("Erstes Ticket", week23.Tickets[0].Subject);
        Assert.Equal(200, week23.Tickets[1].IssueId);
        Assert.Equal(3.0, week23.Tickets[1].Hours);

        var week22 = rows.Single(row => row.Week == ISOWeek.GetWeekOfYear(new DateTime(2026, 5, 26)));
        Assert.Equal(4.0, week22.Hours);
        Assert.Single(week22.Tickets);
        Assert.Equal("Drittes Ticket", week22.Tickets[0].Subject);
    }

    [Fact]
    public void ResolveSubject_UsesIssueFromEntryBeforeCache()
    {
        var entry = new TimeEntryDto
        {
            Issue_Id = 42,
            Issue = new TimeEntryIssueDto { Id = 42, Subject = "Aus API" }
        };

        var subject = WeeklySummaryBuilder.ResolveSubject(
            42,
            entry,
            new Dictionary<int, string> { [42] = "Aus Cache" });

        Assert.Equal("Aus API", subject);
    }

    [Fact]
    public void ResolveSubject_FallsBackToUnknownTicket()
    {
        var entry = new TimeEntryDto { Issue_Id = 99 };

        var subject = WeeklySummaryBuilder.ResolveSubject(99, entry, new Dictionary<int, string>());

        Assert.Equal("Unbekanntes Ticket", subject);
    }

    private static TimeEntryDto CreateEntry(int id, int issueId, string spentOn, double hours) =>
        new()
        {
            Id = id,
            Issue_Id = issueId,
            Spent_On = spentOn,
            Hours = hours
        };
}
