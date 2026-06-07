namespace EasyRedmineTool.Tests;

using EasyRedmineTool.Core.Api;
using EasyRedmineTool.Core.Models.Tickets;

using System.Net;
using System.Text.Json;

public class EasyRedmineApiClientTests
{
    [Fact]
    public async Task GetAllMyOpenIssuesAsync_fetches_all_pages()
    {
        var requestCount = 0;
        var handler = new CountingHandler(_ =>
        {
            requestCount++;
            var response = requestCount switch
            {
                1 => CreateIssuesPage(offset: 0, count: 100, total: 150, startId: 1),
                2 => CreateIssuesPage(offset: 100, count: 50, total: 150, startId: 101),
                _ => CreateIssuesPage(offset: 0, count: 0, total: 150, startId: 0)
            };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(response))
            };
        });

        var client = new EasyRedmineApiClient(new HttpClient(handler));
        var issues = await client.GetAllMyOpenIssuesAsync("https://redmine.example/", "secret");

        Assert.Equal(2, requestCount);
        Assert.Equal(150, issues.Count);
        Assert.Equal(1, issues[0].Id);
        Assert.Equal(150, issues[^1].Id);
    }

    [Fact]
    public async Task GetAllMyOpenIssuesAsync_stops_when_total_count_is_missing()
    {
        var requestCount = 0;
        var handler = new CountingHandler(_ =>
        {
            requestCount++;
            var response = CreateIssuesPage(offset: (requestCount - 1) * 100, count: 100, total: null, startId: (requestCount - 1) * 100 + 1);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(response))
            };
        });

        var client = new EasyRedmineApiClient(new HttpClient(handler));
        var issues = await client.GetAllMyOpenIssuesAsync("https://redmine.example/", "secret");

        Assert.Equal(100, requestCount);
        Assert.Equal(10_000, issues.Count);
    }

    private static IssueListResponse CreateIssuesPage(int offset, int count, int? total, int startId)
    {
        var issues = count == 0
            ? []
            : Enumerable.Range(startId, count)
                .Select(id => new IssueDto { Id = id, Subject = $"Ticket {id}" })
                .ToList();

        return new IssueListResponse
        {
            Issues = issues,
            Total_Count = total ?? 0,
            Offset = offset,
            Limit = 100
        };
    }

    private sealed class CountingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
