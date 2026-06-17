namespace EasyRedmineTool.Core.Services;

using EasyRedmineTool.Core.Models.TimeEntries;

using System.Collections.Concurrent;
using System.Globalization;

internal sealed class TimeEntryFormDataCache
{
    private readonly ConcurrentDictionary<string, IReadOnlyList<TimeEntryActivityDto>> _activities = new();
    private readonly ConcurrentDictionary<string, Task<IReadOnlyList<TimeEntryActivityDto>>> _activitiesInflight = new();
    private readonly ConcurrentDictionary<string, IReadOnlyList<TimeEntryCustomFieldDefinitionDto>> _customFieldDefinitions = new();
    private readonly ConcurrentDictionary<string, Task<IReadOnlyList<TimeEntryCustomFieldDefinitionDto>>> _customFieldDefinitionsInflight = new();

    public async Task<IReadOnlyList<TimeEntryActivityDto>> GetOrLoadActivitiesAsync(
        string cacheKey,
        Func<CancellationToken, Task<IReadOnlyList<TimeEntryActivityDto>>> loader,
        CancellationToken cancellationToken)
    {
        if (_activities.TryGetValue(cacheKey, out var cachedActivities))
        {
            return cachedActivities;
        }

        var loadTask = _activitiesInflight.GetOrAdd(
            cacheKey,
            _ => loader(CancellationToken.None));

        try
        {
            var loaded = await loadTask;
            _activities[cacheKey] = loaded;
            return loaded;
        }
        finally
        {
            _activitiesInflight.TryRemove(cacheKey, out _);
        }
    }

    public async Task<IReadOnlyList<TimeEntryCustomFieldDefinitionDto>> GetOrLoadCustomFieldDefinitionsAsync(
        string cacheKey,
        Func<CancellationToken, Task<IReadOnlyList<TimeEntryCustomFieldDefinitionDto>>> loader,
        CancellationToken cancellationToken)
    {
        if (_customFieldDefinitions.TryGetValue(cacheKey, out var cachedDefinitions))
        {
            return cachedDefinitions;
        }

        var loadTask = _customFieldDefinitionsInflight.GetOrAdd(
            cacheKey,
            _ => loader(CancellationToken.None));

        try
        {
            var loaded = await loadTask;
            _customFieldDefinitions[cacheKey] = loaded;
            return loaded;
        }
        finally
        {
            _customFieldDefinitionsInflight.TryRemove(cacheKey, out _);
        }
    }

    public void Invalidate()
    {
        _activities.Clear();
        _customFieldDefinitions.Clear();
    }

    public static string BuildActivitiesKey(string baseUrl, string apiKey, int? issueId, int? projectId) =>
        $"{baseUrl}|{apiKey}|issue:{issueId?.ToString(CultureInfo.InvariantCulture) ?? "-"}|project:{projectId?.ToString(CultureInfo.InvariantCulture) ?? "-"}";

    public static string BuildCustomFieldDefinitionsKey(string baseUrl, string apiKey, int? projectId) =>
        $"{baseUrl}|{apiKey}|project:{projectId?.ToString(CultureInfo.InvariantCulture) ?? "-"}";
}
