namespace EasyRedmineTool.Core.Api;

using EasyRedmineTool.Core.Models.TimeEntries;
using EasyRedmineTool.Core.Models.Tickets;

public interface IEasyRedmineApiClient
{
    Task<HttpResponseMessage> GetCurrentUserAsync(string baseUrl, string apiKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IssueDto>> GetAllMyOpenIssuesAsync(
        string baseUrl,
        string apiKey,
        CancellationToken cancellationToken = default);

    Task<IssueResponse?> GetIssueByIdAsync(
        string baseUrl,
        string apiKey,
        int issueId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TimeEntryDto>> GetAllMyTimeEntriesAsync(
        string baseUrl,
        string apiKey,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IssueDto>> GetIssuesByIdsAsync(
        string baseUrl,
        string apiKey,
        IReadOnlyList<int> issueIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TimeEntryActivityDto>> GetTimeEntryActivitiesAsync(
        string baseUrl,
        string apiKey,
        int? issueId = null,
        int? projectId = null,
        CancellationToken cancellationToken = default);

    Task<HttpResponseMessage> CreateTimeEntryAsync(
        string baseUrl,
        string apiKey,
        TimeEntryCreateRequest request,
        CancellationToken cancellationToken = default);
}
