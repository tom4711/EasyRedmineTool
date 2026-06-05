namespace EasyRedmineTool.Core.Services.Interfaces;

using EasyRedmineTool.Core.Models.TimeEntries;

public interface ITimeEntryService
{
    Task<TimeEntryOperationResult> CreateTimeEntryAsync(
        string baseUrl,
        string apiKey,
        TimeEntryCreateRequest request,
        CancellationToken cancellationToken = default);
}
