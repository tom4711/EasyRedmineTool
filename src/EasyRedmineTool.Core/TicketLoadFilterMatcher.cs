namespace EasyRedmineTool.Core;

using EasyRedmineTool.Core.Models.Tickets;

public static class TicketLoadFilterMatcher
{
    public static bool Matches(IssueDto issue, TicketLoadFilter filter, int? currentUserId)
    {
        if (!MatchesAssignee(issue, filter.Assignee, currentUserId))
        {
            return false;
        }

        if (!MatchesStatus(issue, filter))
        {
            return false;
        }

        return MatchesLastBookedUntil(issue, filter.LastBookedUntil);
    }

    public static bool MatchesAssignee(IssueDto issue, TicketAssigneeFilter filter, int? currentUserId) =>
        filter switch
        {
            TicketAssigneeFilter.Me => currentUserId.HasValue && issue.Assigned_To?.Id == currentUserId.Value,
            TicketAssigneeFilter.Unassigned => issue.Assigned_To is null,
            TicketAssigneeFilter.All => true,
            _ => true
        };

    public static bool MatchesStatus(IssueDto issue, TicketLoadFilter filter) =>
        filter.StatusKind switch
        {
            TicketStatusFilterKind.Open => issue.Status is null || !issue.Status.Is_Closed,
            TicketStatusFilterKind.Closed => issue.Status?.Is_Closed == true,
            TicketStatusFilterKind.Specific => issue.Status?.Id == filter.StatusId,
            _ => true
        };

    public static bool MatchesLastBookedUntil(IssueDto issue, DateTime? lastBookedUntil)
    {
        if (!lastBookedUntil.HasValue)
        {
            return true;
        }

        if (!issue.LastTimeEntryOn.HasValue)
        {
            return true;
        }

        return issue.LastTimeEntryOn.Value.Date <= lastBookedUntil.Value.Date;
    }
}
