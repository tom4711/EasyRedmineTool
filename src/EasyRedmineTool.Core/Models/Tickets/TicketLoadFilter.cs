namespace EasyRedmineTool.Core.Models.Tickets;

public class TicketLoadFilter
{
    public TicketAssigneeFilter Assignee { get; set; } = TicketAssigneeFilter.Me;

    public TicketStatusFilterKind StatusKind { get; set; } = TicketStatusFilterKind.Open;

    public int? StatusId { get; set; }

    public bool OnlyTimeEntryTickets { get; set; }

    public int TimeEntryLookbackMonths { get; set; } = TicketLoadFilterDefaults.DefaultTimeEntryLookbackMonths;
}
