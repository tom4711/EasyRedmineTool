namespace EasyRedmineTool.Core.Services;

using EasyRedmineTool.Core.Api;
using EasyRedmineTool.Core.Models.TimeEntries;
using EasyRedmineTool.Core.Services.Interfaces;

using Microsoft.Extensions.Logging;

public class TimeEntryService(
    IEasyRedmineApiClient apiClient,
    ITicketService ticketService,
    ILogger<TimeEntryService> logger) : ITimeEntryService
{
    private readonly IEasyRedmineApiClient _apiClient = apiClient;
    private readonly ITicketService _ticketService = ticketService;
    private readonly ILogger<TimeEntryService> _logger = logger;
    private readonly TimeEntryFormDataCache _formDataCache = new();

    public Task<IReadOnlyList<TimeEntryCustomFieldValueDto>> GetRecentCustomFieldValuesAsync(
        string baseUrl,
        string apiKey,
        int? issueId = null,
        int? projectId = null,
        CancellationToken cancellationToken = default) =>
        _apiClient.GetRecentTimeEntryCustomFieldValuesAsync(baseUrl, apiKey, issueId, projectId, cancellationToken);

    public async Task<IReadOnlyList<TimeEntryActivityDto>> GetActivitiesAsync(
        string baseUrl,
        string apiKey,
        int? issueId = null,
        int? projectId = null,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = TimeEntryFormDataCache.BuildActivitiesKey(baseUrl, apiKey, issueId, projectId);

        try
        {
            return await _formDataCache.GetOrLoadActivitiesAsync(
                cacheKey,
                token => _apiClient.GetTimeEntryActivitiesAsync(baseUrl, apiKey, issueId, projectId, token),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (RedmineApiException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Zeiteintrag-Aktivitäten konnten nicht geladen werden (Issue {IssueId}, Projekt {ProjectId}).", issueId, projectId);
            return [];
        }
    }

    public async Task<TimeEntryLoadResult> GetMyTimeEntriesAsync(
        string baseUrl,
        string apiKey,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entries = await _apiClient.GetAllMyTimeEntriesAsync(baseUrl, apiKey, from, to, cancellationToken);
            return new TimeEntryLoadResult { Success = true, Entries = entries };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Zeiteinträge konnten nicht geladen werden ({From:yyyy-MM-dd} bis {To:yyyy-MM-dd}).", from, to);
            return new TimeEntryLoadResult
            {
                Success = false,
                Message = $"Zeiteinträge konnten nicht geladen werden: {ex.Message}"
            };
        }
    }

    public Task<TimeEntryOperationResult> CreateTimeEntryAsync(
        string baseUrl,
        string apiKey,
        TimeEntryCreateRequest request,
        CancellationToken cancellationToken = default) =>
        ExecuteTimeEntryMutationAsync(
            () => _apiClient.CreateTimeEntryAsync(baseUrl, apiKey, request, cancellationToken),
            request.IssueId,
            "Zeiteintrag wurde erstellt.",
            "Zeiteintrag fehlgeschlagen");

    public Task<TimeEntryOperationResult> UpdateTimeEntryAsync(
        string baseUrl,
        string apiKey,
        int timeEntryId,
        TimeEntryUpdateRequest request,
        CancellationToken cancellationToken = default) =>
        ExecuteTimeEntryMutationAsync(
            () => _apiClient.UpdateTimeEntryAsync(baseUrl, apiKey, timeEntryId, request, cancellationToken),
            request.IssueId,
            "Zeiteintrag wurde aktualisiert.",
            "Aktualisierung fehlgeschlagen");

    public Task<TimeEntryOperationResult> DeleteTimeEntryAsync(
        string baseUrl,
        string apiKey,
        int timeEntryId,
        CancellationToken cancellationToken = default) =>
        ExecuteTimeEntryMutationAsync(
            () => _apiClient.DeleteTimeEntryAsync(baseUrl, apiKey, timeEntryId, cancellationToken),
            issueId: null,
            successMessage: "Zeiteintrag wurde gelöscht.",
            failurePrefix: "Löschen fehlgeschlagen");

    private async Task<TimeEntryOperationResult> ExecuteTimeEntryMutationAsync(
        Func<Task<HttpResponseMessage>> sendRequest,
        int? issueId,
        string successMessage,
        string failurePrefix)
    {
        try
        {
            using var response = await sendRequest();

            if (response.IsSuccessStatusCode)
            {
                _ticketService.InvalidateTimeEntryCache();
                _formDataCache.Invalidate();

                return new TimeEntryOperationResult
                {
                    Success = true,
                    Message = successMessage
                };
            }

            var error = await response.Content.ReadAsStringAsync();
            if (issueId.HasValue)
            {
                _logger.LogWarning(
                    "Zeiteintrag für Issue {IssueId} fehlgeschlagen: {StatusCode} {ReasonPhrase}",
                    issueId.Value,
                    (int)response.StatusCode,
                    response.ReasonPhrase);
            }
            else
            {
                _logger.LogWarning(
                    "Zeiteintrag-Änderung fehlgeschlagen: {StatusCode} {ReasonPhrase}",
                    (int)response.StatusCode,
                    response.ReasonPhrase);
            }

            return new TimeEntryOperationResult
            {
                Success = false,
                Message = $"{failurePrefix}: {(int)response.StatusCode} {response.ReasonPhrase} {error}".Trim()
            };
        }
        catch (Exception ex)
        {
            if (issueId.HasValue)
            {
                _logger.LogError(ex, "Zeiteintrag für Issue {IssueId} konnte nicht verarbeitet werden.", issueId.Value);
            }
            else
            {
                _logger.LogError(ex, "Zeiteintrag konnte nicht verarbeitet werden.");
            }

            return new TimeEntryOperationResult
            {
                Success = false,
                Message = $"Fehler: {ex.Message}"
            };
        }
    }
}
