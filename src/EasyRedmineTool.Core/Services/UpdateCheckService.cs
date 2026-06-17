namespace EasyRedmineTool.Core.Services;

using EasyRedmineTool.Core.Models;
using EasyRedmineTool.Core.Services.Interfaces;

using Microsoft.Extensions.Logging;

using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class UpdateCheckService(HttpClient httpClient, ILogger<UpdateCheckService> logger) : IUpdateCheckService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient = httpClient;
    private readonly ILogger<UpdateCheckService> _logger = logger;

    public async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, AppInfo.GitHubReleasesLatestApiUrl);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue(AppInfo.AppName, AppVersion.Current));

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Update-Check fehlgeschlagen: {StatusCode} {ReasonPhrase}",
                    (int)response.StatusCode,
                    response.ReasonPhrase);
                return UpdateCheckResult.UpToDate;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var release = await JsonSerializer.DeserializeAsync<GitHubReleaseResponse>(stream, JsonOptions, cancellationToken);
            if (release is null
                || string.IsNullOrWhiteSpace(release.TagName)
                || string.IsNullOrWhiteSpace(release.HtmlUrl))
            {
                return UpdateCheckResult.UpToDate;
            }

            if (!AppVersion.IsNewer(release.TagName, AppVersion.Current))
            {
                return UpdateCheckResult.UpToDate;
            }

            return new UpdateCheckResult
            {
                IsUpdateAvailable = true,
                LatestVersion = NormalizeTag(release.TagName),
                ReleaseUrl = release.HtmlUrl,
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Update-Check fehlgeschlagen.");
            return UpdateCheckResult.UpToDate;
        }
    }

    private static string NormalizeTag(string tagName)
    {
        var cleaned = tagName.Trim();
        return cleaned.StartsWith("v", StringComparison.OrdinalIgnoreCase)
            ? cleaned[1..]
            : cleaned;
    }

    private sealed class GitHubReleaseResponse
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }
    }
}
