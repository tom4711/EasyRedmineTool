namespace EasyRedmineTool.Tests;

using EasyRedmineTool.Core.Api;
using EasyRedmineTool.Core.Models.TimeEntries;
using EasyRedmineTool.Core.Models.Tickets;
using EasyRedmineTool.Core.Services;

public class TicketServiceCacheTests
{
    [Fact]
    public async Task InvalidateTimeEntryCache_forces_fresh_time_entry_load()
    {
        var apiClient = new CountingTimeEntryApiClient();
        var service = new TicketService(apiClient);

        await service.GetTicketsForListAsync("https://redmine.example/", "secret");
        await service.GetTicketsForListAsync("https://redmine.example/", "secret");
        service.InvalidateTimeEntryCache();
        await service.GetTicketsForListAsync("https://redmine.example/", "secret");

        Assert.Equal(2, apiClient.TimeEntryRequestCount);
    }

    private sealed class CountingTimeEntryApiClient : IEasyRedmineApiClient
    {
        public int TimeEntryRequestCount { get; private set; }

        public Task<HttpResponseMessage> GetCurrentUserAsync(string baseUrl, string apiKey, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<IssueDto>> GetAllMyOpenIssuesAsync(string baseUrl, string apiKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<IssueDto>>([]);

        public Task<IssueResponse?> GetIssueByIdAsync(string baseUrl, string apiKey, int issueId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<TimeEntryDto>> GetAllMyTimeEntriesAsync(
            string baseUrl,
            string apiKey,
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken = default)
        {
            TimeEntryRequestCount++;
            return Task.FromResult<IReadOnlyList<TimeEntryDto>>([]);
        }

        public Task<IReadOnlyList<IssueDto>> GetIssuesByIdsAsync(
            string baseUrl,
            string apiKey,
            IReadOnlyList<int> issueIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<IssueDto>>([]);

        public Task<IReadOnlyList<TimeEntryActivityDto>> GetTimeEntryActivitiesAsync(
            string baseUrl,
            string apiKey,
            int? issueId = null,
            int? projectId = null,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<HttpResponseMessage> CreateTimeEntryAsync(
            string baseUrl,
            string apiKey,
            TimeEntryCreateRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }
}
