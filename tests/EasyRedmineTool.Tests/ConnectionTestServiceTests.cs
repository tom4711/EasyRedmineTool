namespace EasyRedmineTool.Tests;

using EasyRedmineTool.Core.Api;
using EasyRedmineTool.Core.Models;
using EasyRedmineTool.Core.Models.TimeEntries;
using EasyRedmineTool.Core.Models.Tickets;
using EasyRedmineTool.Core.Services;

using Microsoft.Extensions.Logging.Abstractions;

using System.Net;

public class ConnectionTestServiceTests
{
    [Fact]
    public async Task TestConnectionAsync_returns_error_when_base_url_missing()
    {
        var service = CreateService(new FakeApiClient());

        var result = await service.TestConnectionAsync(new ConnectionTestRequest
        {
            BaseUrl = "",
            ApiKey = "secret"
        });

        Assert.False(result.Success);
        Assert.Equal("BaseUrl fehlt.", result.Message);
    }

    [Fact]
    public async Task TestConnectionAsync_returns_error_when_api_key_missing()
    {
        var service = CreateService(new FakeApiClient());

        var result = await service.TestConnectionAsync(new ConnectionTestRequest
        {
            BaseUrl = "https://redmine.example/",
            ApiKey = ""
        });

        Assert.False(result.Success);
        Assert.Equal("API-Key fehlt.", result.Message);
    }

    [Fact]
    public async Task TestConnectionAsync_returns_success_for_ok_response()
    {
        var service = CreateService(new FakeApiClient
        {
            CurrentUserResponse = new HttpResponseMessage(HttpStatusCode.OK)
        });

        var result = await service.TestConnectionAsync(new ConnectionTestRequest
        {
            BaseUrl = "https://redmine.example/",
            ApiKey = "secret"
        });

        Assert.True(result.Success);
        Assert.Equal("Verbindung erfolgreich.", result.Message);
    }

    [Fact]
    public async Task TestConnectionAsync_returns_error_for_failed_response()
    {
        var service = CreateService(new FakeApiClient
        {
            CurrentUserResponse = new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                ReasonPhrase = "Unauthorized"
            }
        });

        var result = await service.TestConnectionAsync(new ConnectionTestRequest
        {
            BaseUrl = "https://redmine.example/",
            ApiKey = "secret"
        });

        Assert.False(result.Success);
        Assert.Contains("401", result.Message, StringComparison.Ordinal);
        Assert.Contains("Unauthorized", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TestConnectionAsync_returns_error_when_api_call_throws()
    {
        var service = CreateService(new FakeApiClient
        {
            CurrentUserException = new HttpRequestException("network down")
        });

        var result = await service.TestConnectionAsync(new ConnectionTestRequest
        {
            BaseUrl = "https://redmine.example/",
            ApiKey = "secret"
        });

        Assert.False(result.Success);
        Assert.Contains("network down", result.Message, StringComparison.Ordinal);
    }

    private static ConnectionTestService CreateService(FakeApiClient apiClient) =>
        new(apiClient, NullLogger<ConnectionTestService>.Instance);

    private sealed class FakeApiClient : IEasyRedmineApiClient
    {
        public HttpResponseMessage CurrentUserResponse { get; set; } = new(HttpStatusCode.OK);
        public Exception? CurrentUserException { get; set; }

        public Task<HttpResponseMessage> GetCurrentUserAsync(string baseUrl, string apiKey, CancellationToken cancellationToken = default)
        {
            if (CurrentUserException is not null)
            {
                throw CurrentUserException;
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

        public Task<IReadOnlyList<TimeEntryCustomFieldDefinitionDto>> GetTimeEntryCustomFieldDefinitionsAsync(
            string baseUrl,
            string apiKey,
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
