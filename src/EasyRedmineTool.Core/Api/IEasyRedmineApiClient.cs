namespace EasyRedmineTool.Core.Api;

using EasyRedmineTool.Core.Models.TimeEntries;
using EasyRedmineTool.Core.Models.Tickets;

using TicketAssigneeFilter = EasyRedmineTool.Core.Models.Tickets.TicketAssigneeFilter;
using TicketStatusFilterKind = EasyRedmineTool.Core.Models.Tickets.TicketStatusFilterKind;

public interface IEasyRedmineApiClient
{
    Task<HttpResponseMessage> GetCurrentUserAsync(string baseUrl, string apiKey, CancellationToken cancellationToken = default);

    Task<int?> GetCurrentUserIdAsync(string baseUrl, string apiKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StatusDto>> GetIssueStatusesAsync(
        string baseUrl,
        string apiKey,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IssueDto>> GetIssuesAsync(
        string baseUrl,
        string apiKey,
        TicketAssigneeFilter assigneeFilter,
        TicketStatusFilterKind statusKind,
        int? statusId = null,
        CancellationToken cancellationToken = default);

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

    Task<IReadOnlyList<TimeEntryCustomFieldValueDto>> GetRecentTimeEntryCustomFieldValuesAsync(
        string baseUrl,
        string apiKey,
        int? issueId = null,
        int? projectId = null,
        int? activityId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TimeEntryCustomFieldDefinitionDto>> GetTimeEntryCustomFieldDefinitionsAsync(
        string baseUrl,
        string apiKey,
        int? projectId = null,
        CancellationToken cancellationToken = default);

    Task<HttpResponseMessage> CreateTimeEntryAsync(
        string baseUrl,
        string apiKey,
        TimeEntryCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<HttpResponseMessage> UpdateTimeEntryAsync(
        string baseUrl,
        string apiKey,
        int timeEntryId,
        TimeEntryUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task<HttpResponseMessage> DeleteTimeEntryAsync(
        string baseUrl,
        string apiKey,
        int timeEntryId,
        CancellationToken cancellationToken = default);
}
