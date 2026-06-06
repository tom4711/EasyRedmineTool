namespace EasyRedmineTool.Core.Services;

using EasyRedmineTool.Core.Api;
using EasyRedmineTool.Core.Models.TimeEntries;
using EasyRedmineTool.Core.Services.Interfaces;

using Microsoft.Extensions.Logging;

public class TimeEntryService(EasyRedmineApiClient apiClient, ILogger<TimeEntryService> logger) : ITimeEntryService
{
    private readonly EasyRedmineApiClient _apiClient = apiClient;
    private readonly ILogger<TimeEntryService> _logger = logger;

    public async Task<IReadOnlyList<TimeEntryActivityDto>> GetActivitiesAsync(
        string baseUrl,
        string apiKey,
        int? issueId = null,
        int? projectId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _apiClient.GetTimeEntryActivitiesAsync(baseUrl, apiKey, issueId, projectId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Zeiteintrag-Aktivitäten konnten nicht geladen werden (Issue {IssueId}, Projekt {ProjectId}).", issueId, projectId);
            return [];
        }
    }

    public async Task<IReadOnlyList<TimeEntryDto>> GetMyTimeEntriesAsync(
        string baseUrl,
        string apiKey,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _apiClient.GetAllMyTimeEntriesAsync(baseUrl, apiKey, from, to, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Zeiteinträge konnten nicht geladen werden ({From:yyyy-MM-dd} bis {To:yyyy-MM-dd}).", from, to);
            return [];
        }
    }

    public async Task<TimeEntryOperationResult> CreateTimeEntryAsync(
        string baseUrl,
        string apiKey,
        TimeEntryCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _apiClient.CreateTimeEntryAsync(baseUrl, apiKey, request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return new TimeEntryOperationResult
                {
                    Success = true,
                    Message = "Zeiteintrag wurde erstellt."
                };
            }

            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "Zeiteintrag für Issue {IssueId} fehlgeschlagen: {StatusCode} {ReasonPhrase}",
                request.IssueId,
                (int)response.StatusCode,
                response.ReasonPhrase);

            return new TimeEntryOperationResult
            {
                Success = false,
                Message = $"Zeiteintrag fehlgeschlagen: {(int)response.StatusCode} {response.ReasonPhrase} {error}".Trim()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Zeiteintrag für Issue {IssueId} konnte nicht erstellt werden.", request.IssueId);
            return new TimeEntryOperationResult
            {
                Success = false,
                Message = $"Fehler: {ex.Message}"
            };
        }
    }
}
