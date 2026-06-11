namespace EasyRedmineTool.Core.Services;

using EasyRedmineTool.Core.Models.TimeEntries;

using System.Collections.Concurrent;
using System.Globalization;

internal sealed class TimeEntryFormDataCache
{
    private readonly ConcurrentDictionary<string, IReadOnlyList<TimeEntryActivityDto>> _activities = new();
    private readonly ConcurrentDictionary<string, Task<IReadOnlyList<TimeEntryActivityDto>>> _activitiesInflight = new();
    private readonly ConcurrentDictionary<string, IReadOnlyList<TimeEntryCustomFieldDefinitionDto>> _allCustomFieldDefinitions = new();
    private readonly ConcurrentDictionary<string, Task<IReadOnlyList<TimeEntryCustomFieldDefinitionDto>>> _definitionsInflight = new();

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
            _ => loader(cancellationToken));

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
        if (_allCustomFieldDefinitions.TryGetValue(cacheKey, out var cachedDefinitions))
        {
            return cachedDefinitions;
        }

        var loadTask = _definitionsInflight.GetOrAdd(
            cacheKey,
            _ => loader(cancellationToken));

        try
        {
            var loaded = await loadTask;
            _allCustomFieldDefinitions[cacheKey] = loaded;
            return loaded;
        }
        finally
        {
            _definitionsInflight.TryRemove(cacheKey, out _);
        }
    }

    public void Invalidate()
    {
        _activities.Clear();
        _allCustomFieldDefinitions.Clear();
    }

    public static string BuildActivitiesKey(string baseUrl, string apiKey, int? issueId, int? projectId) =>
        $"{baseUrl}|{apiKey}|issue:{issueId?.ToString(CultureInfo.InvariantCulture) ?? "-"}|project:{projectId?.ToString(CultureInfo.InvariantCulture) ?? "-"}";

    public static string BuildDefinitionsKey(string baseUrl, string apiKey) =>
        $"{baseUrl}|{apiKey}|custom-field-definitions";

    public static IReadOnlyList<TimeEntryCustomFieldDefinitionDto> FilterDefinitionsForProject(
        IReadOnlyList<TimeEntryCustomFieldDefinitionDto> definitions,
        int? projectId) =>
        definitions
            .Where(definition => AppliesToProject(definition, projectId))
            .ToList();

    private static bool AppliesToProject(TimeEntryCustomFieldDefinitionDto definition, int? projectId)
    {
        if (definition.Project_Ids.Count == 0)
        {
            return true;
        }

        return projectId.HasValue && definition.Project_Ids.Contains(projectId.Value);
    }
}
