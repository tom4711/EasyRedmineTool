namespace EasyRedmineTool.Tests;

using EasyRedmineTool.Core.Configuration;
using EasyRedmineTool.Core.Models.TimeEntries;
using EasyRedmineTool.Core.Models.Tickets;
using EasyRedmineTool.Core.Services;
using EasyRedmineTool.Core.Services.Interfaces;

using Microsoft.Extensions.Logging.Abstractions;

using System.Net;

public class TimeEntryServiceTests
{
    [Fact]
    public async Task CreateTimeEntryAsync_invalidates_ticket_cache_on_success()
    {
        var apiClient = new FakeApiClient
        {
            CreateResponse = new HttpResponseMessage(HttpStatusCode.Created)
        };
        var ticketService = new RecordingTicketService();
        var service = new TimeEntryService(apiClient, ticketService, NullLogger<TimeEntryService>.Instance);

        var result = await service.CreateTimeEntryAsync(
            "https://redmine.example/",
            "secret",
            new TimeEntryCreateRequest
            {
                IssueId = 42,
                Hours = 1,
                SpentOn = "2026-06-07",
                ActivityId = 9,
                Comments = "Test"
            });

        Assert.True(result.Success);
        Assert.Equal(1, ticketService.InvalidateCount);
    }

    [Fact]
    public async Task CreateTimeEntryAsync_does_not_invalidate_ticket_cache_on_failure()
    {
        var apiClient = new FakeApiClient
        {
            CreateResponse = new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
            {
                Content = new StringContent("validation failed")
            }
        };
        var ticketService = new RecordingTicketService();
        var service = new TimeEntryService(apiClient, ticketService, NullLogger<TimeEntryService>.Instance);

        var result = await service.CreateTimeEntryAsync(
            "https://redmine.example/",
            "secret",
            new TimeEntryCreateRequest
            {
                IssueId = 42,
                Hours = 1,
                SpentOn = "2026-06-07",
                ActivityId = 9
            });

        Assert.False(result.Success);
        Assert.Equal(0, ticketService.InvalidateCount);
    }

    [Fact]
    public async Task GetMyTimeEntriesAsync_returns_failure_result_on_exception()
    {
        var apiClient = new FakeApiClient
        {
            GetTimeEntriesException = new HttpRequestException("network down")
        };
        var service = new TimeEntryService(apiClient, new RecordingTicketService(), NullLogger<TimeEntryService>.Instance);

        var result = await service.GetMyTimeEntriesAsync(
            "https://redmine.example/",
            "secret",
            DateTime.Today,
            DateTime.Today);

        Assert.False(result.Success);
        Assert.Contains("network down", result.Message, StringComparison.Ordinal);
    }

    private sealed class FakeApiClient : Core.Api.IEasyRedmineApiClient
    {
        public HttpResponseMessage CreateResponse { get; set; } = new(HttpStatusCode.OK);
        public Exception? GetTimeEntriesException { get; set; }

        public Task<HttpResponseMessage> GetCurrentUserAsync(string baseUrl, string apiKey, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<IssueDto>> GetAllMyOpenIssuesAsync(string baseUrl, string apiKey, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IssueResponse?> GetIssueByIdAsync(string baseUrl, string apiKey, int issueId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<TimeEntryDto>> GetAllMyTimeEntriesAsync(
            string baseUrl,
            string apiKey,
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken = default)
        {
            if (GetTimeEntriesException is not null)
            {
                throw GetTimeEntriesException;
            }

            return Task.FromResult<IReadOnlyList<TimeEntryDto>>([]);
        }

        public Task<IReadOnlyList<IssueDto>> GetIssuesByIdsAsync(
            string baseUrl,
            string apiKey,
            IReadOnlyList<int> issueIds,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<TimeEntryActivityDto>> GetTimeEntryActivitiesAsync(
            string baseUrl,
            string apiKey,
            int? issueId = null,
            int? projectId = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TimeEntryActivityDto>>([]);

        public Task<HttpResponseMessage> CreateTimeEntryAsync(
            string baseUrl,
            string apiKey,
            TimeEntryCreateRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateResponse);
    }

    private sealed class RecordingTicketService : ITicketService
    {
        public int InvalidateCount { get; private set; }

        public void InvalidateTimeEntryCache() => InvalidateCount++;

        public Task<IReadOnlyList<IssueDto>> GetMyOpenIssuesAsync(string baseUrl, string apiKey, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<TicketListLoadResult> GetTicketsForListAsync(string baseUrl, string apiKey, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IssueDto?> GetIssueByIdAsync(string baseUrl, string apiKey, int issueId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }
}
