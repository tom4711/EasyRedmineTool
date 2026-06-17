namespace EasyRedmineTool.Core.Services.Interfaces;

using EasyRedmineTool.Core.Configuration;
using EasyRedmineTool.Core.Models.TimeEntries;
using EasyRedmineTool.Core.ViewModels;

public interface ITimeEntryService
{
    Task<IReadOnlyList<TimeEntryActivityDto>> GetActivitiesAsync(
        string baseUrl,
        string apiKey,
        int? issueId = null,
        int? projectId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TimeEntryCustomFieldValueDto>> GetRecentCustomFieldValuesAsync(
        string baseUrl,
        string apiKey,
        int? issueId = null,
        int? projectId = null,
        int? activityId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TimeEntryCustomFieldRowViewModel>> GetCustomFieldRowsAsync(
        AppSettings settings,
        int? issueId = null,
        int? projectId = null,
        int? activityId = null,
        IReadOnlyList<TimeEntryCustomFieldValueDto>? existingValues = null,
        CancellationToken cancellationToken = default);

    Task<TimeEntryLoadResult> GetMyTimeEntriesAsync(
        string baseUrl,
        string apiKey,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);

    Task<TimeEntryOperationResult> CreateTimeEntryAsync(
        string baseUrl,
        string apiKey,
        TimeEntryCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<TimeEntryOperationResult> UpdateTimeEntryAsync(
        string baseUrl,
        string apiKey,
        int timeEntryId,
        TimeEntryUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task<TimeEntryOperationResult> DeleteTimeEntryAsync(
        string baseUrl,
        string apiKey,
        int timeEntryId,
        CancellationToken cancellationToken = default);
}
