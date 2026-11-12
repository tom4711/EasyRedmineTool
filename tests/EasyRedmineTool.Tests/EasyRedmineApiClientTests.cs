namespace EasyRedmineTool.Tests;

using EasyRedmineTool.Core.Api;
using EasyRedmineTool.Core.Models.Tickets;

using Microsoft.Extensions.Logging;

using System.Globalization;
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
    public async Task GetAllMyOpenIssuesAsync_reports_incomplete_fetch_at_safety_limit()
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
        var error = await Assert.ThrowsAsync<RedmineApiException>(() =>
            client.GetAllMyOpenIssuesAsync("https://redmine.example/", "secret"));

        Assert.Equal(100, requestCount);
        Assert.Contains("unvollständig", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetIssuesAsync_throws_redmine_api_exception_on_http_error()
    {
        var handler = new CountingHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var client = new EasyRedmineApiClient(new HttpClient(handler));

        var exception = await Assert.ThrowsAsync<RedmineApiException>(() =>
            client.GetIssuesAsync(
                "https://redmine.example/",
                "secret",
                TicketAssigneeFilter.Me,
                TicketStatusFilterKind.Open));

        Assert.Equal(401, exception.StatusCode);
        Assert.Contains("issues.json", exception.Endpoint, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetTimeEntryActivitiesAsync_throws_when_all_endpoints_fail()
    {
        var handler = new CountingHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));
        var client = new EasyRedmineApiClient(new HttpClient(handler));

        var exception = await Assert.ThrowsAsync<RedmineApiException>(() =>
            client.GetTimeEntryActivitiesAsync("https://redmine.example/", "secret"));

        Assert.Equal(403, exception.StatusCode);
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

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public async Task Pagination_honors_server_page_size(bool timeEntries, bool includeTotal)
    {
        var offsets = new List<int>();
        var handler = new CountingHandler(request =>
        {
            var query = request.RequestUri!.Query.TrimStart('?').Split('&')
                .Select(part => part.Split('=')).ToDictionary(part => part[0], part => part[1]);
            var offset = int.Parse(query["offset"], CultureInfo.InvariantCulture);
            offsets.Add(offset);
            var count = Math.Min(25, Math.Max(0, 80 - offset));
            var items = Enumerable.Range(offset + 1, count).Select(id => new { id }).ToList();
            var response = new Dictionary<string, object>
            {
                [timeEntries ? "time_entries" : "issues"] = items,
                ["limit"] = 25,
                ["offset"] = offset
            };
            if (includeTotal) response["total_count"] = 80;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(response))
            };
        });
        var client = new EasyRedmineApiClient(new HttpClient(handler));

        var ids = timeEntries
            ? (await client.GetAllMyTimeEntriesAsync("https://redmine.example/", "secret", DateTime.Today, DateTime.Today)).Select(x => x.Id)
            : (await client.GetAllMyOpenIssuesAsync("https://redmine.example/", "secret")).Select(x => x.Id);

        Assert.Equal(Enumerable.Range(1, 80), ids);
        Assert.Equal(new[] { 0, 25, 50, 75 }, offsets);
    }

    [Fact]
    public async Task Time_entries_report_incomplete_fetch_at_safety_limit()
    {
        var requests = 0;
        var handler = new CountingHandler(_ =>
        {
            var offset = requests++ * 100;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    time_entries = Enumerable.Range(offset + 1, 100).Select(id => new { id }),
                    offset, limit = 100, total_count = 10_001
                }))
            };
        });
        var client = new EasyRedmineApiClient(new HttpClient(handler));
        var error = await Assert.ThrowsAsync<RedmineApiException>(() =>
            client.GetAllMyTimeEntriesAsync("https://redmine.example/", "secret", DateTime.Today, DateTime.Today));
        Assert.Equal(100, requests);
        Assert.Contains("unvollständig", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Pagination_without_total_supports_full_page_and_empty_end(bool timeEntries)
    {
        var requests = 0;
        var handler = new CountingHandler(_ =>
        {
            var offset = requests++ * 100;
            var response = new Dictionary<string, object>
            {
                [timeEntries ? "time_entries" : "issues"] = Enumerable.Range(1, offset == 0 ? 100 : 0).Select(id => new { id }),
                ["offset"] = offset, ["limit"] = 100
            };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(response)) };
        });
        var client = new EasyRedmineApiClient(new HttpClient(handler));
        var count = timeEntries
            ? (await client.GetAllMyTimeEntriesAsync("https://redmine.example/", "secret", DateTime.Today, DateTime.Today)).Count
            : (await client.GetAllMyOpenIssuesAsync("https://redmine.example/", "secret")).Count;
        Assert.Equal(100, count);
        Assert.Equal(2, requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Pagination_reports_repeated_page_instead_of_counting_duplicate_ids(bool timeEntries)
    {
        var requests = 0;
        var handler = new CountingHandler(_ =>
        {
            requests++;
            var response = new Dictionary<string, object>
            {
                [timeEntries ? "time_entries" : "issues"] = Enumerable.Range(1, 25).Select(id => new { id }),
                ["offset"] = 0, ["limit"] = 25, ["total_count"] = 80
            };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(response)) };
        });
        var client = new EasyRedmineApiClient(new HttpClient(handler));
        await Assert.ThrowsAsync<RedmineApiException>(async () =>
        {
            if (timeEntries) await client.GetAllMyTimeEntriesAsync("https://redmine.example/", "secret", DateTime.Today, DateTime.Today);
            else await client.GetAllMyOpenIssuesAsync("https://redmine.example/", "secret");
        });
        Assert.Equal(2, requests);
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("en-US")]
    [InlineData("th-TH")]
    public async Task Time_entry_query_uses_gregorian_dates(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            string? query = null;
            var client = new EasyRedmineApiClient(new HttpClient(new CountingHandler(request =>
            {
                query = request.RequestUri!.Query;
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"time_entries\":[]}") };
            })));
            await client.GetAllMyTimeEntriesAsync("https://redmine.example/", "secret", new DateTime(2026, 11, 1), new DateTime(2026, 11, 1));

            Assert.Contains("from=2026-11-01", query, StringComparison.Ordinal);
            Assert.Contains("to=2026-11-01", query, StringComparison.Ordinal);
        }
        finally { CultureInfo.CurrentCulture = previous; }
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
