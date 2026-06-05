namespace EasyRedmineTool.Core.Api;

using EasyRedmineTool.Core.Models.Tickets;

using System.Net.Http.Headers;
using System.Net.Http.Json;

public class EasyRedmineApiClient
{
    private readonly HttpClient _httpClient;

    public EasyRedmineApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    private void Configure(string baseUrl, string apiKey)
    {
        _httpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Add("X-Redmine-API-Key", apiKey);
    }

    public async Task<HttpResponseMessage> GetCurrentUserAsync(
        string baseUrl,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        Configure(baseUrl, apiKey);
        return await _httpClient.GetAsync("users/current.json", cancellationToken);
    }

    public async Task<IssueListResponse?> GetMyOpenIssuesAsync(
        string baseUrl,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        Configure(baseUrl, apiKey);

        return await _httpClient.GetFromJsonAsync<IssueListResponse>(
            "issues.json?assigned_to_id=me&status_id=open&limit=100",
            cancellationToken);
    }
}
