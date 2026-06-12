namespace EasyRedmineTool.Tests;

using EasyRedmineTool.Core;
using EasyRedmineTool.Core.Models.Tickets;

public class TicketCacheMergerTests
{
    [Fact]
    public void Merge_keeps_favorited_tickets_not_in_latest_load()
    {
        var existing = new[]
        {
            new IssueDto { Id = 1, Subject = "Favorite old" },
            new IssueDto { Id = 2, Subject = "Stale non-favorite" }
        };
        var loaded = new[] { new IssueDto { Id = 3, Subject = "New load" } };
        var favorites = new HashSet<int> { 1 };

        var merged = TicketCacheMerger.Merge(loaded, existing, favorites);

        Assert.Equal(2, merged.Count);
        Assert.Contains(merged, ticket => ticket.Id == 1 && ticket.Subject == "Favorite old");
        Assert.Contains(merged, ticket => ticket.Id == 3 && ticket.Subject == "New load");
        Assert.DoesNotContain(merged, ticket => ticket.Id == 2);
    }

    [Fact]
    public void Merge_updates_favorited_ticket_when_it_is_in_latest_load()
    {
        var existing = new[] { new IssueDto { Id = 1, Subject = "Old subject" } };
        var loaded = new[] { new IssueDto { Id = 1, Subject = "Fresh subject" } };
        var favorites = new HashSet<int> { 1 };

        var merged = TicketCacheMerger.Merge(loaded, existing, favorites);

        Assert.Single(merged);
        Assert.Equal("Fresh subject", merged[0].Subject);
    }
}
