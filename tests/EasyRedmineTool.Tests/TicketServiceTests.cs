namespace EasyRedmineTool.Tests;

using EasyRedmineTool.Core.Models.TimeEntries;
using EasyRedmineTool.Core.Services;

public class TicketServiceTests
{
    [Fact]
    public void BuildLastTimeEntryLookup_returns_latest_date_per_issue()
    {
        var entries = new List<TimeEntryDto>
        {
            CreateEntry(issueId: 1, spentOn: "2025-01-10"),
            CreateEntry(issueId: 1, spentOn: "2025-03-01"),
            CreateEntry(issueId: 2, spentOn: "2025-02-15"),
            CreateEntry(issueId: 3, spentOn: "invalid"),
            CreateEntry(issueId: 0, spentOn: "2025-01-01"),
        };

        var lookup = TicketService.BuildLastTimeEntryLookup(entries);

        Assert.Equal(2, lookup.Count);
        Assert.Equal(new DateTime(2025, 3, 1), lookup[1]);
        Assert.Equal(new DateTime(2025, 2, 15), lookup[2]);
    }

    private static TimeEntryDto CreateEntry(int issueId, string spentOn) => new()
    {
        Issue_Id = issueId,
        Spent_On = spentOn,
    };
}
