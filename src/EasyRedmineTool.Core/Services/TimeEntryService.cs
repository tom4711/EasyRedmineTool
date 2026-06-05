namespace EasyRedmineTool.Core.Services;

using EasyRedmineTool.Core.Api;
using EasyRedmineTool.Core.Models.TimeEntries;
using EasyRedmineTool.Core.Services.Interfaces;

public class TimeEntryService : ITimeEntryService
{
    private readonly EasyRedmineApiClient _apiClient;

    public TimeEntryService(EasyRedmineApiClient apiClient)
    {
        _apiClient = apiClient;
    }

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
        catch
        {
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
            return new TimeEntryOperationResult
            {
                Success = false,
                Message = $"Zeiteintrag fehlgeschlagen: {(int)response.StatusCode} {response.ReasonPhrase} {error}".Trim()
            };
        }
        catch (Exception ex)
        {
            return new TimeEntryOperationResult
            {
                Success = false,
                Message = $"Fehler: {ex.Message}"
            };
        }
    }
}
