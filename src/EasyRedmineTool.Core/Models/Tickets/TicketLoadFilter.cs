namespace EasyRedmineTool.Core.Models.Tickets;

using EasyRedmineTool.Core.Services;

public class TicketLoadFilter
{
    public TicketAssigneeFilter Assignee { get; set; } = TicketAssigneeFilter.Me;

    public TicketStatusFilterKind StatusKind { get; set; } = TicketStatusFilterKind.Open;

    public int? StatusId { get; set; }

    public bool IncludeTimeEntryTickets { get; set; }

    public int TimeEntryLookbackMonths { get; set; } = TicketService.DefaultTimeEntryLookbackMonths;
}
