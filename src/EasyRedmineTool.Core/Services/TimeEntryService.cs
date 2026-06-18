namespace EasyRedmineTool.Core.Services;

using EasyRedmineTool.Core;
using EasyRedmineTool.Core.Api;
using EasyRedmineTool.Core.Configuration;
using EasyRedmineTool.Core.Models.TimeEntries;
using EasyRedmineTool.Core.Services.Interfaces;
using EasyRedmineTool.Core.ViewModels;

using Microsoft.Extensions.Logging;

public class TimeEntryService(
    IEasyRedmineApiClient apiClient,
    ITicketService ticketService,
    IAppSettingsService appSettingsService,
    ILogger<TimeEntryService> logger) : ITimeEntryService
{
    private readonly IEasyRedmineApiClient _apiClient = apiClient;
    private readonly ITicketService _ticketService = ticketService;
    private readonly IAppSettingsService _appSettingsService = appSettingsService;
    private readonly ILogger<TimeEntryService> _logger = logger;
    private readonly TimeEntryFormDataCache _formDataCache = new();

    public Task<IReadOnlyList<TimeEntryCustomFieldValueDto>> GetRecentCustomFieldValuesAsync(
        string baseUrl,
        string apiKey,
        int? issueId = null,
        int? projectId = null,
        int? activityId = null,
        CancellationToken cancellationToken = default) =>
        _apiClient.GetRecentTimeEntryCustomFieldValuesAsync(baseUrl, apiKey, issueId, projectId, activityId, cancellationToken);

    public async Task<IReadOnlyList<TimeEntryCustomFieldRowViewModel>> GetCustomFieldRowsAsync(
        AppSettings settings,
        int? issueId = null,
        int? projectId = null,
        int? activityId = null,
        string? activityName = null,
        IReadOnlyList<TimeEntryCustomFieldValueDto>? existingValues = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            return [];
        }

        var settingsDirectory = Path.GetDirectoryName(_appSettingsService.SettingsFilePath);
        var configuredRules = TimeEntryCustomFieldSettingsRules.Resolve(
            settings,
            activityId,
            activityName,
            settingsDirectory);
        IReadOnlyList<TimeEntryCustomFieldDefinitionDto> definitions;

        if (configuredRules.Definitions.Count > 0)
        {
            definitions = configuredRules.Definitions;
        }
        else
        {
            definitions = await GetCustomFieldDefinitionsAsync(
                settings.BaseUrl,
                settings.ApiKey,
                issueId,
                projectId,
                activityId,
                cancellationToken);

            if (definitions.Count == 0 && issueId.HasValue && activityId.HasValue)
            {
                definitions = await ProbeCustomFieldDefinitionsAsync(
                    settings,
                    issueId.Value,
                    projectId,
                    activityId.Value,
                    cancellationToken);
            }
        }

        IReadOnlyList<TimeEntryCustomFieldValueDto> recentValues = [];
        if (configuredRules.Definitions.Count == 0)
        {
            try
            {
                recentValues = await GetRecentCustomFieldValuesAsync(
                    settings.BaseUrl,
                    settings.ApiKey,
                    issueId,
                    projectId,
                    activityId,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (RedmineApiException)
            {
                // Recent values are optional when definitions are available.
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Letzte Custom-Field-Werte konnten nicht geladen werden (Issue {IssueId}, Projekt {ProjectId}).",
                    issueId,
                    projectId);
            }
        }

        var mergedExisting = MergeConfiguredValues(existingValues, configuredRules.DefaultValues);

        return TimeEntryCustomFieldSupport.CreateRows(
            definitions,
            recentValues,
            settings,
            projectId,
            activityId,
            mergedExisting);
    }

    public Task ResolveCustomFieldIdsAsync(
        AppSettings settings,
        ICollection<TimeEntryCustomFieldRowViewModel> rows,
        CancellationToken cancellationToken = default)
    {
        foreach (var row in rows.ToList())
        {
            if (row.Id > 0)
            {
                continue;
            }

            var resolvedId = ResolveKnownCustomFieldId(settings, row.Name);
            if (resolvedId is not > 0)
            {
                continue;
            }

            rows.Remove(row);
            rows.Add(TimeEntryCustomFieldSupport.CreateProbedRow(resolvedId.Value, row.Name, row.Value, row.IsMultiple));
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> TryAddMissingCustomFieldsFromBookingErrorAsync(
        AppSettings settings,
        ICollection<TimeEntryCustomFieldRowViewModel> rows,
        string bookingErrorMessage,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        var requiredNames = TimeEntryCustomFieldProbe.ParseRequiredFieldNamesFromMessage(bookingErrorMessage);
        if (requiredNames.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        var hintId = TimeEntryCustomFieldProbe.TryParseFieldIdFromError(bookingErrorMessage);
        var addedNames = new List<string>();

        foreach (var name in requiredNames)
        {
            if (rows.Any(row => string.Equals(row.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var resolvedId = hintId ?? ResolveKnownCustomFieldId(settings, name) ?? 0;

            if (resolvedId > 0)
            {
                PersistResolvedCustomFieldId(name, resolvedId);
            }

            var isMultiple = ResolveIsMultipleFromSettings(settings, name);
            rows.Add(TimeEntryCustomFieldSupport.CreateProbedRow(resolvedId, name, isMultiple: isMultiple));
            addedNames.Add(name);
        }

        return Task.FromResult<IReadOnlyList<string>>(addedNames);
    }

    private static bool ResolveIsMultipleFromSettings(AppSettings settings, string fieldName) =>
        settings.TimeEntryCustomFieldActivityRules
            .SelectMany(rule => rule.Fields)
            .FirstOrDefault(field => string.Equals(field.Name, fieldName, StringComparison.OrdinalIgnoreCase))
            ?.IsMultiple ?? false;

    private async Task<IReadOnlyList<TimeEntryCustomFieldDefinitionDto>> GetCustomFieldDefinitionsAsync(
        string baseUrl,
        string apiKey,
        int? issueId,
        int? projectId,
        int? activityId,
        CancellationToken cancellationToken)
    {
        var cacheKey = TimeEntryFormDataCache.BuildCustomFieldDefinitionsKey(
            baseUrl,
            apiKey,
            issueId,
            projectId,
            activityId);

        try
        {
            return await _formDataCache.GetOrLoadCustomFieldDefinitionsAsync(
                cacheKey,
                token => _apiClient.GetTimeEntryCustomFieldDefinitionsAsync(
                    baseUrl,
                    apiKey,
                    issueId,
                    projectId,
                    activityId,
                    token),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Custom-Field-Definitionen konnten nicht geladen werden (Projekt {ProjectId}).",
                projectId);
            return [];
        }
    }

    private async Task<IReadOnlyList<TimeEntryCustomFieldDefinitionDto>> ProbeCustomFieldDefinitionsAsync(
        AppSettings settings,
        int issueId,
        int? projectId,
        int activityId,
        CancellationToken cancellationToken)
    {
        try
        {
            var requiredNames = await _apiClient.ProbeRequiredTimeEntryCustomFieldNamesAsync(
                settings.BaseUrl,
                settings.ApiKey,
                issueId,
                activityId,
                cancellationToken);

            if (requiredNames.Count == 0)
            {
                return [];
            }

            var definitions = new List<TimeEntryCustomFieldDefinitionDto>();
            foreach (var name in requiredNames)
            {
                var id = ResolveKnownCustomFieldId(settings, name) ?? 0;

                if (id > 0)
                {
                    PersistResolvedCustomFieldId(name, id);
                }

                definitions.Add(TimeEntryCustomFieldDefinitionFactory.CreateProbedDefinition(id, name, activityId));
            }

            return definitions;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Pflicht-Custom-Fields konnten nicht per Validierung ermittelt werden (Issue {IssueId}, Aktivität {ActivityId}).",
                issueId,
                activityId);
            return [];
        }
    }

    private static int? ResolveKnownCustomFieldId(AppSettings settings, string fieldName) =>
        settings.TimeEntryCustomFieldIdMappings
            .FirstOrDefault(mapping => string.Equals(mapping.Name, fieldName, StringComparison.OrdinalIgnoreCase))
            ?.Id
        ?? TimeEntryCustomFieldSettingsRules.ResolveFieldId(settings, fieldName);

    private static IReadOnlyList<TimeEntryCustomFieldValueDto>? MergeConfiguredValues(
        IReadOnlyList<TimeEntryCustomFieldValueDto>? existingValues,
        IReadOnlyList<TimeEntryCustomFieldValueDto> configuredValues)
    {
        if (configuredValues.Count == 0)
        {
            return existingValues;
        }

        var merged = existingValues?.ToList() ?? [];
        foreach (var value in configuredValues)
        {
            if (merged.All(existing => existing.Id != value.Id))
            {
                merged.Add(value);
            }
        }

        return merged;
    }

    private void PersistResolvedCustomFieldId(string fieldName, int id)
    {
        _appSettingsService.Update(settings =>
        {
            if (settings.TimeEntryCustomFieldIdMappings.Any(
                    mapping => string.Equals(mapping.Name, fieldName, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            settings.TimeEntryCustomFieldIdMappings.Add(new TimeEntryCustomFieldIdMapping
            {
                Name = fieldName,
                Id = id
            });
        });
    }

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
