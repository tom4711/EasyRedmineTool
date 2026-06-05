namespace EasyRedmineTool.Core.Api;

using EasyRedmineTool.Core.Models.TimeEntries;
using EasyRedmineTool.Core.Models.Tickets;

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

public class EasyRedmineApiClient
{
    private readonly HttpClient _httpClient;

    public EasyRedmineApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<HttpResponseMessage> GetCurrentUserAsync(
        string baseUrl,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        var request = CreateRequest(HttpMethod.Get, baseUrl, apiKey, "users/current.json");
        return await _httpClient.SendAsync(request, cancellationToken);
    }

    public async Task<IssueListResponse?> GetMyOpenIssuesAsync(
        string baseUrl,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, baseUrl, apiKey, "issues.json?assigned_to_id=me&status_id=open&limit=100");
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<IssueListResponse>(cancellationToken);
    }

    public async Task<IssueResponse?> GetIssueByIdAsync(
        string baseUrl,
        string apiKey,
        int issueId,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, baseUrl, apiKey, $"issues/{issueId}.json");
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<IssueResponse>(cancellationToken);
    }

    public async Task<TimeEntriesListResponse?> GetMyTimeEntriesAsync(
        string baseUrl,
        string apiKey,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        var endpoint = $"time_entries.json?user_id=me&from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}&limit=500";
        using var request = CreateRequest(HttpMethod.Get, baseUrl, apiKey, endpoint);
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<TimeEntriesListResponse>(cancellationToken);
    }

    public async Task<IReadOnlyList<TimeEntryActivityDto>> GetTimeEntryActivitiesAsync(
        string baseUrl,
        string apiKey,
        int? issueId = null,
        int? projectId = null,
        CancellationToken cancellationToken = default)
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

    public async Task<HttpResponseMessage> CreateTimeEntryAsync(
        string baseUrl,
        string apiKey,
        TimeEntryCreateRequest request,
        CancellationToken cancellationToken = default)
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

        var message = CreateRequest(HttpMethod.Post, baseUrl, apiKey, "time_entries.json");
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
    {
        if (source.TryGetProperty(propertyName, out value)
            && value.ValueKind == JsonValueKind.Array)
        {
            return true;
        }

        return false;
    }
}
