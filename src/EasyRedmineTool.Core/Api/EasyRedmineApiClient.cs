using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace EasyRedmineTool.Core.Api;

public class EasyRedmineApiClient
{
    private readonly HttpClient _httpClient;

    public EasyRedmineApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<HttpResponseMessage> GetCurrentUserAsync(string baseUrl, string apiKey, CancellationToken cancellationToken = default)
    {
        _httpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Add("X-Redmine-API-Key", apiKey);

        return await _httpClient.GetAsync("users/current.json", cancellationToken);
    }
}
