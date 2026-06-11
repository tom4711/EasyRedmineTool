namespace EasyRedmineTool.Core;

using EasyRedmineTool.Core.Models.Tickets;

public static class TicketCacheMerger
{
    public static List<IssueDto> Merge(
        IEnumerable<IssueDto> loadedTickets,
        IEnumerable<IssueDto> existingCachedTickets,
        IReadOnlySet<int> favoriteTicketIds)
    {
        var merged = new Dictionary<int, IssueDto>();

        foreach (var ticket in loadedTickets)
        {
            merged[ticket.Id] = ticket;
        }

        foreach (var ticket in existingCachedTickets)
        {
            if (favoriteTicketIds.Contains(ticket.Id) && !merged.ContainsKey(ticket.Id))
            {
                merged[ticket.Id] = ticket;
            }
        }

        return merged.Values
            .OrderBy(ticket => ticket.Id)
            .ToList();
    }
}
