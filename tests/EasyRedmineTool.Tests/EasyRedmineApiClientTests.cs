namespace EasyRedmineTool.Tests;

using EasyRedmineTool.Core.Api;
using EasyRedmineTool.Core.Models.Tickets;

using Microsoft.Extensions.Logging;

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

    [Fact]
    public async Task DeleteTimeEntryAsync_completes_successfully()
    {
        var handler = new CountingHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = new EasyRedmineApiClient(new HttpClient(handler));

        using var response = await client.DeleteTimeEntryAsync("https://redmine.example/", "secret", 42);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Theory]
    [InlineData(TicketAssigneeFilter.Me, TicketStatusFilterKind.Open, null, "assigned_to_id=me", "status_id=open")]
    [InlineData(TicketAssigneeFilter.Unassigned, TicketStatusFilterKind.Closed, null, "assigned_to_id=!*", "status_id=closed")]
    [InlineData(TicketAssigneeFilter.All, TicketStatusFilterKind.All, null, "status_id=*")]
    [InlineData(TicketAssigneeFilter.Me, TicketStatusFilterKind.Specific, 3, "assigned_to_id=me", "status_id=3")]
    public void BuildIssuesQuery_includes_selected_assignee_and_status_filters(
        TicketAssigneeFilter assigneeFilter,
        TicketStatusFilterKind statusKind,
        int? statusId,
        params string[] expectedParts)
    {
        var query = EasyRedmineApiClient.BuildIssuesQuery(assigneeFilter, statusKind, statusId, limit: 25, offset: 50);

        Assert.StartsWith("issues.json?", query, StringComparison.Ordinal);
        Assert.Contains("limit=25", query, StringComparison.Ordinal);
        Assert.Contains("offset=50", query, StringComparison.Ordinal);

        foreach (var expectedPart in expectedParts)
        {
            Assert.Contains(expectedPart, query, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task GetIssuesByIdsAsync_falls_back_to_single_issue_requests_when_batch_fails()
    {
        var handler = new CountingHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/issues.json", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }

            if (path.EndsWith("/issues/42.json", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new IssueResponse
                    {
                        Issue = new IssueDto { Id = 42, Subject = "Fallback" }
                    }))
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var client = new EasyRedmineApiClient(new HttpClient(handler));
        var issues = await client.GetIssuesByIdsAsync("https://redmine.example/", "secret", [42]);

        Assert.Single(issues);
        Assert.Equal(42, issues[0].Id);
        Assert.Equal("Fallback", issues[0].Subject);
    }

    [Fact]
    public async Task GetIssuesByIdsAsync_logs_unresolved_issue_ids()
    {
        var logger = new TestLogger<EasyRedmineApiClient>();
        var handler = new CountingHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var client = new EasyRedmineApiClient(new HttpClient(handler), logger);

        var issues = await client.GetIssuesByIdsAsync("https://redmine.example/", "secret", [7, 8]);

        Assert.Empty(issues);
        Assert.Contains(logger.Entries, entry =>
            entry.Level == LogLevel.Warning &&
            entry.Message.Contains("Issues konnten nicht geladen werden", StringComparison.Ordinal) &&
            entry.Message.Contains('7') &&
            entry.Message.Contains('8'));
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

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();
            public void Dispose() { }
        }
    }
}
