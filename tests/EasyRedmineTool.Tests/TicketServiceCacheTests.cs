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

        var filter = new TicketLoadFilter { OnlyTimeEntryTickets = true };
        await service.GetTicketsForListAsync("https://redmine.example/", "secret", filter);
        await service.GetTicketsForListAsync("https://redmine.example/", "secret", filter);
        service.InvalidateTimeEntryCache();
        await service.GetTicketsForListAsync("https://redmine.example/", "secret", filter);

        Assert.Equal(2, apiClient.TimeEntryRequestCount);
    }

    [Fact]
    public async Task GetTicketsForListAsync_without_time_entries_does_not_query_time_entries()
    {
        var apiClient = new CountingTimeEntryApiClient();
        var service = new TicketService(apiClient);

        await service.GetTicketsForListAsync(
            "https://redmine.example/",
            "secret",
            new TicketLoadFilter { OnlyTimeEntryTickets = false });

        Assert.Equal(0, apiClient.TimeEntryRequestCount);
    }

    [Fact]
    public async Task Invalidation_during_load_prevents_old_cache_publication_and_sharing()
    {
        var oldGate = new TaskCompletionSource<IReadOnlyList<TimeEntryDto>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var freshGate = new TaskCompletionSource<IReadOnlyList<TimeEntryDto>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var api = new CountingTimeEntryApiClient { Load = count => count == 1 ? oldGate.Task : freshGate.Task };
        var service = new TicketService(api);
        var filter = new TicketLoadFilter { OnlyTimeEntryTickets = true };
        var oldLoad = service.GetTicketsForListAsync("https://redmine.example/", "secret", filter);
        service.InvalidateTimeEntryCache();
        var newLoad = service.GetTicketsForListAsync("https://redmine.example/", "secret", filter);
        oldGate.SetResult([new() { Issue_Id = 1 }]);
        await oldLoad;
        var otherNewReader = service.GetTicketsForListAsync("https://redmine.example/", "secret", filter);
        freshGate.SetResult([new() { Issue_Id = 2 }]);
        Assert.Equal(2, (await newLoad).Tickets[0].Id);
        Assert.Equal(2, (await otherNewReader).Tickets[0].Id);
        Assert.Equal(2, (await service.GetTicketsForListAsync("https://redmine.example/", "secret", filter)).Tickets[0].Id);
        Assert.Equal(2, api.TimeEntryRequestCount);
    }

    [Fact]
    public async Task Concurrent_readers_share_load_and_waiter_cancellation_is_independent()
    {
        var gate = new TaskCompletionSource<IReadOnlyList<TimeEntryDto>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var api = new CountingTimeEntryApiClient { Load = _ => gate.Task };
        var service = new TicketService(api);
        var filter = new TicketLoadFilter { OnlyTimeEntryTickets = true };
        using var cancellation = new CancellationTokenSource();
        var first = service.GetTicketsForListAsync("https://redmine.example/", "secret", filter, cancellation.Token);
        var second = service.GetTicketsForListAsync("https://redmine.example/", "secret", filter);
        cancellation.Cancel();
        gate.SetResult([]);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        await second;
        Assert.Equal(1, api.TimeEntryRequestCount);
    }

    private sealed class CountingTimeEntryApiClient : IEasyRedmineApiClient
    {
        public int TimeEntryRequestCount { get; private set; }
        public Func<int, Task<IReadOnlyList<TimeEntryDto>>>? Load { get; init; }

        public Task<HttpResponseMessage> GetCurrentUserAsync(string baseUrl, string apiKey, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<int?> GetCurrentUserIdAsync(string baseUrl, string apiKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<int?>(1);

        public Task<IReadOnlyList<StatusDto>> GetIssueStatusesAsync(
            string baseUrl,
            string apiKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<StatusDto>>([]);

        public Task<IReadOnlyList<IssueDto>> GetIssuesAsync(
            string baseUrl,
            string apiKey,
            TicketAssigneeFilter assigneeFilter,
            TicketStatusFilterKind statusKind,
            int? statusId = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<IssueDto>>([]);

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
            return Load?.Invoke(TimeEntryRequestCount) ?? Task.FromResult<IReadOnlyList<TimeEntryDto>>([]);
        }

        public Task<IReadOnlyList<IssueDto>> GetIssuesByIdsAsync(
            string baseUrl,
            string apiKey,
            IReadOnlyList<int> issueIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<IssueDto>>(issueIds.Select(id => new IssueDto { Id = id }).ToList());

        public Task<IReadOnlyList<TimeEntryActivityDto>> GetTimeEntryActivitiesAsync(
            string baseUrl,
            string apiKey,
            int? issueId = null,
            int? projectId = null,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<TimeEntryCustomFieldValueDto>> GetRecentTimeEntryCustomFieldValuesAsync(
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

        public Task<HttpResponseMessage> UpdateTimeEntryAsync(
            string baseUrl,
            string apiKey,
            int timeEntryId,
            TimeEntryUpdateRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<HttpResponseMessage> DeleteTimeEntryAsync(
            string baseUrl,
            string apiKey,
            int timeEntryId,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }
}
