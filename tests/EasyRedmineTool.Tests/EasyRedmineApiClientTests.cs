namespace EasyRedmineTool.Tests;

using EasyRedmineTool.Core.Api;
using EasyRedmineTool.Core.Models;
using EasyRedmineTool.Core.Models.TimeEntries;
using EasyRedmineTool.Core.Models.Tickets;
using EasyRedmineTool.Core.Services;

using Microsoft.Extensions.Logging.Abstractions;

using System.Net;

public class EasyRedmineApiClientTests
{
    [Fact]
    public async Task GetCurrentUserIdAsync_returns_null_with_invalid_api_key()
    {
        var apiClient = new FakeApiClient
        {
            CurrentUserResponse = new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                ReasonPhrase = "Unauthorized"
            }
        };

        var client = new EasyRedmineApiClient(apiClient, NullLogger<EasyRedmineApiClient>.Instance);

        var userId = await client.GetCurrentUserIdAsync("https://redmine.example/", "invalid_key");

        Assert.Null(userId);
    }

    [Fact]
    public async Task GetIssuesAsync_returns_empty_list_with_invalid_filter()
    {
        var apiClient = new FakeApiClient
        {
            GetIssuesPageAsync = _ => Task.FromResult(new IssueListResponse
            {
                Issues = [],
                Total_Count = 0,
                Offset = 0,
                Limit = 0
            })
        };

        var client = new EasyRedmineApiClient(apiClient, NullLogger<EasyRedmineApiClient>.Instance);

        var issues = await client.GetIssuesAsync(
            "https://redmine.example/",
            "secret",
            TicketAssigneeFilter.Me,
            TicketStatusFilterKind.Open,
            statusId: 999,
            CancellationToken.None);

        Assert.Empty(issues);
    }

    [Fact]
    public async Task GetTimeEntryActivitiesAsync_returns_empty_list_with_invalid_issue_id()
    {
        var apiClient = new FakeApiClient
        {
            GetMyTimeEntriesPageAsync = _ => Task.FromResult(new TimeEntriesListResponse
            {
                Time_Entries = [],
                Total_Count = 0,
                Offset = 0,
                Limit = 0
            })
        };

        var client = new EasyRedmineApiClient(apiClient, NullLogger<EasyRedmineApiClient>.Instance);

        var activities = await client.GetTimeEntryActivitiesAsync(
            "https://redmine.example/",
            "secret",
            issueId: 999,
            CancellationToken.None);

        Assert.Empty(activities);
    }

    [Fact]
    public async Task GetRecentTimeEntryCustomFieldValuesAsync_returns_empty_list_with_invalid_issue_id()
    {
        var apiClient = new FakeApiClient
        {
            GetMyTimeEntriesPageAsync = _ => Task.FromResult(new TimeEntriesListResponse
            {
                Time_Entries = [],
                Total_Count = 0,
                Offset = 0,
                Limit = 0
            })
        };

        var client = new EasyRedmineApiClient(apiClient, NullLogger<EasyRedmineApiClient>.Instance);

        var customFieldValues = await client.GetRecentTimeEntryCustomFieldValuesAsync(
            "https://redmine.example/",
            "secret",
            issueId: 999,
            CancellationToken.None);

        Assert.Empty(customFieldValues);
    }

    [Fact]
    public async Task CreateTimeEntryAsync_returns_error_with_invalid_request()
    {
        var apiClient = new FakeApiClient
        {
            CreateTimeEntryAsync = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest))
        };

        var client = new EasyRedmineApiClient(apiClient, NullLogger<EasyRedmineApiClient>.Instance);

        var result = await client.CreateTimeEntryAsync(
            "https://redmine.example/",
            "secret",
            new TimeEntryCreateRequest
            {
                IssueId = 1,
                Hours = 1.0,
                SpentOn = "2023-04-01",
                ActivityId = 1,
                Comments = "Test",
                CustomFields = []
            },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    [Fact]
    public async Task UpdateTimeEntryAsync_returns_error_with_invalid_request()
    {
        var apiClient = new FakeApiClient
        {
            UpdateTimeEntryAsync = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest))
        };

        var client = new EasyRedmineApiClient(apiClient, NullLogger<EasyRedmineApiClient>.Instance);

        var result = await client.UpdateTimeEntryAsync(
            "https://redmine.example/",
            "secret",
            1,
            new TimeEntryUpdateRequest
            {
                IssueId = 1,
                Hours = 1.0,
                SpentOn = "2023-04-01",
                ActivityId = 1,
                Comments = "Test",
                CustomFields = []
            },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    [Fact]
    public async Task DeleteTimeEntryAsync_returns_error_with_invalid_time_entry_id()
    {
        var apiClient = new FakeApiClient
        {
            DeleteTimeEntryAsync = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound))
        };

        var client = new EasyRedmineApiClient(apiClient, NullLogger<EasyRedmineApiClient>.Instance);

        var result = await client.DeleteTimeEntryAsync(
            "https://redmine.example/",
            "secret",
            999,
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
    }

    private sealed class FakeApiClient : IEasyRedmineApiClient
    {
        public HttpResponseMessage CurrentUserResponse { get; set; } = new(HttpStatusCode.OK);
        public Func<Task<IssueListResponse>> GetIssuesPageAsync { get; set; } = () => Task.FromResult(new IssueListResponse());
        public Func<Task<TimeEntriesListResponse>> GetMyTimeEntriesPageAsync { get; set; } = () => Task.FromResult(new TimeEntriesListResponse());
        public Func<Task<HttpResponseMessage>> CreateTimeEntryAsync { get; set; } = () => Task.FromResult(new HttpResponseMessage());
        public Func<Task<HttpResponseMessage>> UpdateTimeEntryAsync { get; set; } = () => Task.FromResult(new HttpResponseMessage());
        public Func<Task<HttpResponseMessage>> DeleteTimeEntryAsync { get; set; } = () => Task.FromResult(new HttpResponseMessage());

        public Task<HttpResponseMessage> GetCurrentUserAsync(string baseUrl, string apiKey, CancellationToken cancellationToken = default)
        {
            if (CurrentUserResponse.StatusCode == HttpStatusCode.Unauthorized)
            {
                return Task.FromResult(CurrentUserResponse);
            }

            return Task.FromResult(CurrentUserResponse);
        }

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
            CancellationToken cancellationToken = default)
        {
            if (GetIssuesPageAsync is not null)
            {
                return GetIssuesPageAsync();
            }

            return Task.FromResult<IReadOnlyList<IssueDto>>([]);
        }

        public Task<IReadOnlyList<IssueDto>> GetAllMyOpenIssuesAsync(string baseUrl, string apiKey, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IssueResponse?> GetIssueByIdAsync(string baseUrl, string apiKey, int issueId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<TimeEntryDto>> GetAllMyTimeEntriesAsync(
            string baseUrl,
            string apiKey,
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

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
            CancellationToken cancellationToken = default)
        {
            if (CreateTimeEntryAsync is not null)
            {
                return CreateTimeEntryAsync();
            }

            return Task.FromResult(new HttpResponseMessage());
        }

        public Task<HttpResponseMessage> UpdateTimeEntryAsync(
            string baseUrl,
            string apiKey,
            int timeEntryId,
            TimeEntryUpdateRequest request,
            CancellationToken cancellationToken = default)
        {
            if (UpdateTimeEntryAsync is not null)
            {
                return UpdateTimeEntryAsync();
            }

            return Task.FromResult(new HttpResponseMessage());
        }

        public Task<HttpResponseMessage> DeleteTimeEntryAsync(
            string baseUrl,
            string apiKey,
            int timeEntryId,
            CancellationToken cancellationToken = default)
        {
            if (DeleteTimeEntryAsync is not null)
            {
                return DeleteTimeEntryAsync();
            }

            return Task.FromResult(new HttpResponseMessage());
        }
    }
}
