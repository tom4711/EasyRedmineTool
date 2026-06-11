namespace EasyRedmineTool.Core.Api;

using EasyRedmineTool.Core.Models.TimeEntries;
using EasyRedmineTool.Core.Models.Tickets;
using EasyRedmineTool.Core.Models.Users;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

public class EasyRedmineApiClient(HttpClient httpClient, ILogger<EasyRedmineApiClient>? logger = null) : IEasyRedmineApiClient
{
    private const int PageLimit = 100;
    private const int MaxPaginationPages = 100;

    private readonly HttpClient _httpClient = httpClient;
    private readonly ILogger<EasyRedmineApiClient> _logger = logger ?? NullLogger<EasyRedmineApiClient>.Instance;

    public async Task<HttpResponseMessage> GetCurrentUserAsync(string baseUrl, string apiKey, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, baseUrl, apiKey, "users/current.json");
        return await _httpClient.SendAsync(request, cancellationToken);
    }

    public async Task<int?> GetCurrentUserIdAsync(string baseUrl, string apiKey, CancellationToken cancellationToken = default)
    {
        using var response = await GetCurrentUserAsync(baseUrl, apiKey, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var result = await response.Content.ReadFromJsonAsync<UserResponse>(RedmineJson.Options, cancellationToken);
        return result?.User.Id;
    }

    public Task<IReadOnlyList<IssueDto>> GetAllMyOpenIssuesAsync(
        string baseUrl,
        string apiKey,
        CancellationToken cancellationToken = default) =>
        GetIssuesAsync(baseUrl, apiKey, TicketAssigneeFilter.Me, TicketStatusFilterKind.Open, cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<StatusDto>> GetIssueStatusesAsync(
        string baseUrl,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, baseUrl, apiKey, "issue_statuses.json");
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var result = await response.Content.ReadFromJsonAsync<IssueStatusListResponse>(RedmineJson.Options, cancellationToken);
        return result?.Issue_Statuses ?? [];
    }

    public async Task<IReadOnlyList<IssueDto>> GetIssuesAsync(
        string baseUrl,
        string apiKey,
        TicketAssigneeFilter assigneeFilter,
        TicketStatusFilterKind statusKind,
        int? statusId = null,
        CancellationToken cancellationToken = default)
    {
        var offset = 0;
        var all = new List<IssueDto>();
        int? totalCount = null;

        for (var pageIndex = 0; pageIndex < MaxPaginationPages; pageIndex++)
        {
            var page = await GetIssuesPageAsync(baseUrl, apiKey, assigneeFilter, statusKind, statusId, PageLimit, offset, cancellationToken);
            if (page?.Issues is null || page.Issues.Count == 0)
            {
                break;
            }

            all.AddRange(page.Issues);
            if (page.Total_Count > 0)
            {
                totalCount ??= page.Total_Count;
            }

            if (IsLastPage(page.Issues.Count, PageLimit, all.Count, totalCount))
            {
                break;
            }

            offset += PageLimit;
        }

        return all;
    }

    private async Task<IssueListResponse?> GetIssuesPageAsync(
        string baseUrl,
        string apiKey,
        TicketAssigneeFilter assigneeFilter,
        TicketStatusFilterKind statusKind,
        int? statusId,
        int limit,
        int offset,
        CancellationToken cancellationToken = default)
    {
        var query = BuildIssuesQuery(assigneeFilter, statusKind, statusId, limit, offset);
        using var request = CreateRequest(HttpMethod.Get, baseUrl, apiKey, query);
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<IssueListResponse>(RedmineJson.Options, cancellationToken);
    }

    internal static string BuildIssuesQuery(
        TicketAssigneeFilter assigneeFilter,
        TicketStatusFilterKind statusKind,
        int? statusId,
        int limit,
        int offset)
    {
        var parameters = new List<string>
        {
            $"limit={limit}",
            $"offset={offset}"
        };

        switch (assigneeFilter)
        {
            case TicketAssigneeFilter.Me:
                parameters.Add("assigned_to_id=me");
                break;
            case TicketAssigneeFilter.Unassigned:
                parameters.Add("assigned_to_id=!*");
                break;
        }

        parameters.Add(statusKind switch
        {
            TicketStatusFilterKind.Open => "status_id=open",
            TicketStatusFilterKind.Closed => "status_id=closed",
            TicketStatusFilterKind.Specific when statusId.HasValue => $"status_id={statusId.Value}",
            _ => "status_id=*"
        });

        return $"issues.json?{string.Join('&', parameters)}";
    }

    public async Task<IssueResponse?> GetIssueByIdAsync(string baseUrl, string apiKey, int issueId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, baseUrl, apiKey, $"issues/{issueId}.json");
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<IssueResponse>(RedmineJson.Options, cancellationToken);
    }

    public async Task<IReadOnlyList<TimeEntryDto>> GetAllMyTimeEntriesAsync(
        string baseUrl,
        string apiKey,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        var offset = 0;
        var all = new List<TimeEntryDto>();
        int? totalCount = null;

        for (var pageIndex = 0; pageIndex < MaxPaginationPages; pageIndex++)
        {
            var page = await GetMyTimeEntriesPageAsync(baseUrl, apiKey, from, to, PageLimit, offset, cancellationToken);
            if (page?.Time_Entries is null || page.Time_Entries.Count == 0)
            {
                break;
            }

            all.AddRange(page.Time_Entries);
            if (page.Total_Count > 0)
            {
                totalCount ??= page.Total_Count;
            }

            if (IsLastPage(page.Time_Entries.Count, PageLimit, all.Count, totalCount))
            {
                break;
            }

            offset += PageLimit;
        }

        return all;
    }

    public async Task<IReadOnlyList<IssueDto>> GetIssuesByIdsAsync(
        string baseUrl,
        string apiKey,
        IReadOnlyList<int> issueIds,
        CancellationToken cancellationToken = default)
    {
        if (issueIds.Count == 0)
        {
            return [];
        }

        var issuesById = new Dictionary<int, IssueDto>();
        const int batchSize = 100;

        for (var offset = 0; offset < issueIds.Count; offset += batchSize)
        {
            var batch = issueIds.Skip(offset).Take(batchSize).ToList();
            var idsParam = string.Join(',', batch);
            var endpoint = $"issues.json?issue_id={idsParam}&status_id=*&limit={batchSize}";
            using var request = CreateRequest(HttpMethod.Get, baseUrl, apiKey, endpoint);
            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Issue-Batch konnte nicht geladen werden ({StatusCode} {ReasonPhrase}): {IssueIds}",
                    (int)response.StatusCode,
                    response.ReasonPhrase,
                    idsParam);
                continue;
            }

            var result = await response.Content.ReadFromJsonAsync<IssueListResponse>(RedmineJson.Options, cancellationToken);
            if (result?.Issues is null)
            {
                _logger.LogWarning(
                    "Issue-Batch lieferte keine parsebare Antwort: {IssueIds}",
                    idsParam);
                continue;
            }

            foreach (var issue in result.Issues)
            {
                issuesById[issue.Id] = issue;
            }
        }

        foreach (var issueId in issueIds)
        {
            if (issuesById.ContainsKey(issueId))
            {
                continue;
            }

            var issueResponse = await GetIssueByIdAsync(baseUrl, apiKey, issueId, cancellationToken);
            if (issueResponse?.Issue is not null)
            {
                issuesById[issueId] = issueResponse.Issue;
            }
        }

        var unresolvedIds = issueIds.Where(id => !issuesById.ContainsKey(id)).ToList();
        if (unresolvedIds.Count > 0)
        {
            _logger.LogWarning(
                "Issues konnten nicht geladen werden: {IssueIds}",
                string.Join(", ", unresolvedIds));
        }

        return issueIds
            .Where(issuesById.ContainsKey)
            .Select(id => issuesById[id])
            .ToList();
    }

    private async Task<TimeEntriesListResponse?> GetMyTimeEntriesPageAsync(
        string baseUrl,
        string apiKey,
        DateTime from,
        DateTime to,
        int limit,
        int offset,
        CancellationToken cancellationToken = default)
    {
        var endpoint = $"time_entries.json?user_id=me&from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}&limit={limit}&offset={offset}&include=issue";
        using var request = CreateRequest(HttpMethod.Get, baseUrl, apiKey, endpoint);
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<TimeEntriesListResponse>(RedmineJson.Options, cancellationToken);
    }

    public async Task<IReadOnlyList<TimeEntryActivityDto>> GetTimeEntryActivitiesAsync( string baseUrl, string apiKey, int? issueId = null, int? projectId = null, CancellationToken cancellationToken = default)
    {
        var endpoints = new List<string>();

        if (issueId.HasValue)
        {
            endpoints.Add($"issues/{issueId.Value}.json?include=time_entry_activities");
            endpoints.Add($"issues/{issueId.Value}/time_entry_activities.json");
        }

        if (projectId.HasValue)
        {
            endpoints.Add($"projects/{projectId.Value}.json?include=time_entry_activities");
            endpoints.Add($"projects/{projectId.Value}/time_entry_activities.json");
        }

        endpoints.Add("enumerations/time_entry_activities.json");

        foreach (var endpoint in endpoints)
        {
            using var request = CreateRequest(HttpMethod.Get, baseUrl, apiKey, endpoint);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                continue;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var parsed = ParseActivities(json);
            if (parsed.Count > 0)
            {
                return parsed;
            }
        }

        return [];
    }

    public async Task<HttpResponseMessage> CreateTimeEntryAsync( string baseUrl, string apiKey, TimeEntryCreateRequest request, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            time_entry = new
            {
                issue_id = request.IssueId,
                hours = request.Hours,
                spent_on = request.SpentOn,
                activity_id = request.ActivityId,
                comments = request.Comments
            }
        };

        using var message = CreateRequest(HttpMethod.Post, baseUrl, apiKey, "time_entries.json");
        message.Content = JsonContent.Create(payload);

        return await _httpClient.SendAsync(message, cancellationToken);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string baseUrl, string apiKey, string relativeOrAbsolutePath)
    {
        var baseUri = new Uri(baseUrl.TrimEnd('/') + "/");
        var targetUri = new Uri(baseUri, relativeOrAbsolutePath);

        var request = new HttpRequestMessage(method, targetUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("X-Redmine-API-Key", apiKey);

        return request;
    }

    private static List<TimeEntryActivityDto> ParseActivities(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (TryReadActivitiesArray(root, out var directActivities))
        {
            return directActivities;
        }

        if (root.TryGetProperty("issue", out var issue) && TryReadActivitiesArray(issue, out var issueActivities))
        {
            return issueActivities;
        }

        if (root.TryGetProperty("project", out var project) && TryReadActivitiesArray(project, out var projectActivities))
        {
            return projectActivities;
        }

        return [];
    }

    private static bool TryReadActivitiesArray(JsonElement source, out List<TimeEntryActivityDto> activities)
    {
        activities = [];

        if (!TryGetArray(source, "time_entry_activities", out var array)
            && !TryGetArray(source, "activities", out array)
            && !TryGetArray(source, "allowed_activities", out array))
        {
            return false;
        }

        foreach (var item in array.EnumerateArray())
        {
            if (!item.TryGetProperty("id", out var idProperty)
                || idProperty.ValueKind != JsonValueKind.Number
                || !idProperty.TryGetInt32(out var id))
            {
                continue;
            }

            var name = item.TryGetProperty("name", out var nameProperty)
                ? nameProperty.GetString() ?? string.Empty
                : string.Empty;

            activities.Add(new TimeEntryActivityDto
            {
                Id = id,
                Name = name
            });
        }

        return true;
    }

    private static bool TryGetArray(JsonElement source, string propertyName, out JsonElement value) 
        => source.TryGetProperty(propertyName, out value) && value.ValueKind == JsonValueKind.Array;

    private static bool IsLastPage(int pageItemCount, int pageLimit, int totalLoaded, int? totalCount) =>
        pageItemCount < pageLimit
        || (totalCount is > 0 && totalLoaded >= totalCount.Value);
}
