namespace EasyRedmineTool.Tests;

using EasyRedmineTool.Core.Api;
using EasyRedmineTool.Core.Configuration;
using EasyRedmineTool.Core.Models.TimeEntries;
using EasyRedmineTool.Core.Models.Tickets;
using EasyRedmineTool.Core.Services;
using EasyRedmineTool.Core.Services.Interfaces;

using Microsoft.Extensions.Logging.Abstractions;

using System.Net;

public class TimeEntryServiceTests
{
    private static TimeEntryService CreateService(IEasyRedmineApiClient apiClient, ITicketService ticketService) =>
        new(apiClient, ticketService, new NoOpAppSettingsService(), NullLogger<TimeEntryService>.Instance);

    [Fact]
    public async Task CreateTimeEntryAsync_invalidates_ticket_cache_on_success()
    {
        var apiClient = new FakeApiClient
        {
            CreateResponse = new HttpResponseMessage(HttpStatusCode.Created)
        };
        var ticketService = new RecordingTicketService();
        var service = CreateService(apiClient, ticketService);

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
        var service = CreateService(apiClient, ticketService);

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
    public async Task UpdateTimeEntryAsync_invalidates_ticket_cache_on_success()
    {
        var apiClient = new FakeApiClient
        {
            CreateResponse = new HttpResponseMessage(HttpStatusCode.OK)
        };
        var ticketService = new RecordingTicketService();
        var service = CreateService(apiClient, ticketService);

        var result = await service.UpdateTimeEntryAsync(
            "https://redmine.example/",
            "secret",
            7,
            new TimeEntryUpdateRequest
            {
                IssueId = 42,
                Hours = 2,
                SpentOn = "2026-06-07",
                ActivityId = 9
            });

        Assert.True(result.Success);
        Assert.Equal(1, ticketService.InvalidateCount);
    }

    [Fact]
    public async Task DeleteTimeEntryAsync_invalidates_ticket_cache_on_success()
    {
        var apiClient = new FakeApiClient
        {
            CreateResponse = new HttpResponseMessage(HttpStatusCode.NoContent)
        };
        var ticketService = new RecordingTicketService();
        var service = CreateService(apiClient, ticketService);

        var result = await service.DeleteTimeEntryAsync("https://redmine.example/", "secret", 7);

        Assert.True(result.Success);
        Assert.Equal(1, ticketService.InvalidateCount);
    }

    [Fact]
    public async Task GetMyTimeEntriesAsync_returns_failure_result_on_exception()
    {
        var apiClient = new FakeApiClient
        {
            GetTimeEntriesException = new HttpRequestException("network down")
        };
        var service = CreateService(apiClient, new RecordingTicketService());

        var result = await service.GetMyTimeEntriesAsync(
            "https://redmine.example/",
            "secret",
            DateTime.Today,
            DateTime.Today);

        Assert.False(result.Success);
        Assert.Contains("network down", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetActivitiesAsync_caches_per_issue_and_project()
    {
        var apiClient = new FakeApiClient();
        var service = CreateService(apiClient, new RecordingTicketService());

        await service.GetActivitiesAsync("https://redmine.example/", "secret", issueId: 1, projectId: 5);
        await service.GetActivitiesAsync("https://redmine.example/", "secret", issueId: 1, projectId: 5);
        await service.GetActivitiesAsync("https://redmine.example/", "secret", issueId: 2, projectId: 9);

        Assert.Equal(2, apiClient.ActivityLoadCount);
    }

    private sealed class FakeApiClient : Core.Api.IEasyRedmineApiClient
    {
        public HttpResponseMessage CreateResponse { get; set; } = new(HttpStatusCode.OK);
        public Exception? GetTimeEntriesException { get; set; }
        public int ActivityLoadCount { get; private set; }

        public Task<HttpResponseMessage> GetCurrentUserAsync(string baseUrl, string apiKey, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<int?> GetCurrentUserIdAsync(string baseUrl, string apiKey, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<StatusDto>> GetIssueStatusesAsync(
            string baseUrl,
            string apiKey,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<IssueDto>> GetIssuesAsync(
            string baseUrl,
            string apiKey,
            TicketAssigneeFilter assigneeFilter,
            TicketStatusFilterKind statusKind,
            int? statusId = null,
            CancellationToken cancellationToken = default) =>
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
            CancellationToken cancellationToken = default)
        {
            ActivityLoadCount++;
            return Task.FromResult<IReadOnlyList<TimeEntryActivityDto>>([]);
        }

        public Task<IReadOnlyList<TimeEntryCustomFieldValueDto>> GetRecentTimeEntryCustomFieldValuesAsync(
            string baseUrl,
            string apiKey,
            int? issueId = null,
            int? projectId = null,
            int? activityId = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TimeEntryCustomFieldValueDto>>([]);

        public Task<IReadOnlyList<TimeEntryCustomFieldDefinitionDto>> GetTimeEntryCustomFieldDefinitionsAsync(
            string baseUrl,
            string apiKey,
            int? issueId = null,
            int? projectId = null,
            int? activityId = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TimeEntryCustomFieldDefinitionDto>>([]);

        public Task<HttpResponseMessage> CreateTimeEntryAsync(
            string baseUrl,
            string apiKey,
            TimeEntryCreateRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateResponse);

        public Task<HttpResponseMessage> UpdateTimeEntryAsync(
            string baseUrl,
            string apiKey,
            int timeEntryId,
            TimeEntryUpdateRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateResponse);

        public Task<HttpResponseMessage> DeleteTimeEntryAsync(
            string baseUrl,
            string apiKey,
            int timeEntryId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateResponse);

        public Task<IReadOnlyList<string>> ProbeRequiredTimeEntryCustomFieldNamesAsync(
            string baseUrl,
            string apiKey,
            int issueId,
            int activityId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<int?> TryResolveTimeEntryCustomFieldIdAsync(
            string baseUrl,
            string apiKey,
            string fieldName,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<int?>(null);

        public Task<IReadOnlyList<string>> SearchTimeEntryCustomFieldValuesAsync(
            string baseUrl,
            string apiKey,
            int customFieldId,
            string query,
            int? issueId = null,
            int? projectId = null,
            int? activityId = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);
    }

    private sealed class NoOpAppSettingsService : IAppSettingsService
    {
        public string SettingsFilePath => string.Empty;

        public AppSettings Load() => new();

        public void Save(AppSettings settings)
        {
        }

        public void Update(Action<AppSettings> configure) => configure(new AppSettings());
    }

    private sealed class RecordingTicketService : ITicketService
    {
        public int InvalidateCount { get; private set; }

        public void InvalidateTimeEntryCache() => InvalidateCount++;

        public Task<IReadOnlyList<IssueDto>> GetMyOpenIssuesAsync(string baseUrl, string apiKey, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<StatusDto>> GetIssueStatusesAsync(
            string baseUrl,
            string apiKey,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<TicketListLoadResult> GetTicketsForListAsync(
            string baseUrl,
            string apiKey,
            TicketLoadFilter filter,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IssueDto?> GetIssueByIdAsync(string baseUrl, string apiKey, int issueId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }
}
