namespace EasyRedmineTool.Core.Models.Tickets;

public class TicketListLoadResult
{
    public IReadOnlyList<IssueDto> Tickets { get; init; } = [];

    public int OpenTicketCount { get; init; }

    public int TimeEntryTicketCount { get; init; }
}
