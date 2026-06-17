namespace EasyRedmineTool.Core.Models.Tickets;

public static class TicketLoadFilterDefaults
{
    public const int DefaultTimeEntryLookbackMonths = 12;

    public static int NormalizeTimeEntryLookbackMonths(int months) => months switch
    {
        3 => 3,
        6 => 6,
        9 => 9,
        _ => DefaultTimeEntryLookbackMonths
    };
}
