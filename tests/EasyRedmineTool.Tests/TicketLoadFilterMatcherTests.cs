namespace EasyRedmineTool.Tests;

using EasyRedmineTool.Core;
using EasyRedmineTool.Core.Models.Tickets;

public class TicketLoadFilterMatcherTests
{
    [Fact]
    public void MatchesLastBookedUntil_includes_unbooked_and_tickets_on_or_before_date()
    {
        var until = new DateTime(2025, 3, 1);

        Assert.True(TicketLoadFilterMatcher.MatchesLastBookedUntil(new IssueDto { Id = 1 }, until));
        Assert.True(TicketLoadFilterMatcher.MatchesLastBookedUntil(
            new IssueDto { Id = 2, LastTimeEntryOn = new DateTime(2025, 3, 1) },
            until));
        Assert.False(TicketLoadFilterMatcher.MatchesLastBookedUntil(
            new IssueDto { Id = 3, LastTimeEntryOn = new DateTime(2025, 3, 2) },
            until));
    }

    [Fact]
    public void MatchesLastBookedUntil_with_booking_data_excludes_unbooked_and_recently_booked_issues()
    {
        var until = new DateTime(2026, 6, 10);
        var bookedAfterFilter = new HashSet<int> { 2, 5 };

        Assert.False(TicketLoadFilterMatcher.MatchesLastBookedUntil(
            new IssueDto { Id = 1 },
            until,
            bookedAfterFilter));
        Assert.False(TicketLoadFilterMatcher.MatchesLastBookedUntil(
            new IssueDto { Id = 2, LastTimeEntryOn = new DateTime(2026, 6, 11) },
            until,
            bookedAfterFilter));
        Assert.True(TicketLoadFilterMatcher.MatchesLastBookedUntil(
            new IssueDto { Id = 3, LastTimeEntryOn = new DateTime(2026, 6, 9) },
            until,
            bookedAfterFilter));
        Assert.True(TicketLoadFilterMatcher.MatchesLastBookedUntil(
            new IssueDto { Id = 4, LastTimeEntryOn = new DateTime(2026, 6, 10) },
            until,
            bookedAfterFilter));
    }

    [Fact]
    public void MatchesAssignee_respects_me_unassigned_and_all()
    {
        var issueAssignedToMe = new IssueDto { Assigned_To = new NamedEntityDto { Id = 7 } };
        var issueUnassigned = new IssueDto();
        var issueAssignedToOther = new IssueDto { Assigned_To = new NamedEntityDto { Id = 9 } };

        Assert.True(TicketLoadFilterMatcher.MatchesAssignee(issueAssignedToMe, TicketAssigneeFilter.Me, 7));
        Assert.False(TicketLoadFilterMatcher.MatchesAssignee(issueAssignedToOther, TicketAssigneeFilter.Me, 7));
        Assert.True(TicketLoadFilterMatcher.MatchesAssignee(issueUnassigned, TicketAssigneeFilter.Unassigned, 7));
        Assert.False(TicketLoadFilterMatcher.MatchesAssignee(issueAssignedToMe, TicketAssigneeFilter.Unassigned, 7));
        Assert.True(TicketLoadFilterMatcher.MatchesAssignee(issueAssignedToOther, TicketAssigneeFilter.All, 7));
    }

    [Fact]
    public void MatchesStatus_respects_open_closed_specific_and_all()
    {
        var openIssue = new IssueDto { Status = new StatusDto { Id = 2, Is_Closed = false } };
        var closedIssue = new IssueDto { Status = new StatusDto { Id = 5, Is_Closed = true } };

        Assert.True(TicketLoadFilterMatcher.MatchesStatus(openIssue, new TicketLoadFilter
        {
            StatusKind = TicketStatusFilterKind.Open
        }));
        Assert.False(TicketLoadFilterMatcher.MatchesStatus(closedIssue, new TicketLoadFilter
        {
            StatusKind = TicketStatusFilterKind.Open
        }));
        Assert.True(TicketLoadFilterMatcher.MatchesStatus(closedIssue, new TicketLoadFilter
        {
            StatusKind = TicketStatusFilterKind.Closed
        }));
        Assert.True(TicketLoadFilterMatcher.MatchesStatus(openIssue, new TicketLoadFilter
        {
            StatusKind = TicketStatusFilterKind.Specific,
            StatusId = 2
        }));
        Assert.False(TicketLoadFilterMatcher.MatchesStatus(openIssue, new TicketLoadFilter
        {
            StatusKind = TicketStatusFilterKind.Specific,
            StatusId = 5
        }));
        Assert.True(TicketLoadFilterMatcher.MatchesStatus(closedIssue, new TicketLoadFilter
        {
            StatusKind = TicketStatusFilterKind.All
        }));
    }

    [Fact]
    public void Matches_combines_all_filters()
    {
        var filter = new TicketLoadFilter
        {
            Assignee = TicketAssigneeFilter.Me,
            StatusKind = TicketStatusFilterKind.Open,
            LastBookedUntil = new DateTime(2025, 6, 1)
        };

        var matchingIssue = new IssueDto
        {
            Assigned_To = new NamedEntityDto { Id = 5 },
            Status = new StatusDto { Is_Closed = false },
            LastTimeEntryOn = new DateTime(2025, 5, 1)
        };

        Assert.True(TicketLoadFilterMatcher.Matches(matchingIssue, filter, 5));
        Assert.False(TicketLoadFilterMatcher.Matches(new IssueDto
        {
            Assigned_To = new NamedEntityDto { Id = 8 },
            Status = new StatusDto { Is_Closed = false },
            LastTimeEntryOn = new DateTime(2025, 5, 1)
        }, filter, 5));
        Assert.False(TicketLoadFilterMatcher.Matches(new IssueDto
        {
            Assigned_To = new NamedEntityDto { Id = 5 },
            Status = new StatusDto { Is_Closed = true },
            LastTimeEntryOn = new DateTime(2025, 5, 1)
        }, filter, 5));
        Assert.False(TicketLoadFilterMatcher.Matches(new IssueDto
        {
            Assigned_To = new NamedEntityDto { Id = 5 },
            Status = new StatusDto { Is_Closed = false },
            LastTimeEntryOn = new DateTime(2025, 6, 2)
        }, filter, 5));
    }
}
