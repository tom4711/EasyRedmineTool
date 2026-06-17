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

        var filter = new TicketLoadFilter { IncludeTimeEntryTickets = true };
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
            new TicketLoadFilter { IncludeTimeEntryTickets = false });

        Assert.Equal(0, apiClient.TimeEntryRequestCount);
    }

    private sealed class CountingTimeEntryApiClient : IEasyRedmineApiClient
    {
        public int TimeEntryRequestCount { get; private set; }

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

        public Task<IReadOnlyList<TimeEntryCustomFieldValueDto>> GetRecentTimeEntryCustomFieldValuesAsync(
            string baseUrl,
            string apiKey,
            int? issueId = null,
            int? projectId = null,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<TimeEntryCustomFieldDefinitionDto>> GetTimeEntryCustomFieldDefinitionsAsync(
            string baseUrl,
            string apiKey,
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
