namespace EasyRedmineTool.Core.Models.Tickets;

public sealed class TicketStatusFilterSelection
{
    private TicketStatusFilterSelection(TicketStatusFilterKind kind, int? statusId, string label)
    {
        Kind = kind;
        StatusId = statusId;
        Label = label;
    }

    public TicketStatusFilterKind Kind { get; }

    public int? StatusId { get; }

    public string Label { get; }

    public static TicketStatusFilterSelection All { get; } =
        new(TicketStatusFilterKind.All, null, "Alle Status");

    public static TicketStatusFilterSelection Open { get; } =
        new(TicketStatusFilterKind.Open, null, "Alle offenen");

    public static TicketStatusFilterSelection Closed { get; } =
        new(TicketStatusFilterKind.Closed, null, "Alle geschlossenen");

    public static TicketStatusFilterSelection FromStatus(StatusDto status) =>
        new(TicketStatusFilterKind.Specific, status.Id, status.Name);

    public static TicketStatusFilterSelection? TryCreate(TicketStatusFilterKind kind, int? statusId, string? statusName)
    {
        return kind switch
        {
            TicketStatusFilterKind.All => All,
            TicketStatusFilterKind.Open => Open,
            TicketStatusFilterKind.Closed => Closed,
            TicketStatusFilterKind.Specific when statusId.HasValue && !string.IsNullOrWhiteSpace(statusName) =>
                new(TicketStatusFilterKind.Specific, statusId.Value, statusName),
            TicketStatusFilterKind.Specific when statusId.HasValue =>
                new(TicketStatusFilterKind.Specific, statusId.Value, $"Status #{statusId.Value}"),
            _ => null
        };
    }
}
