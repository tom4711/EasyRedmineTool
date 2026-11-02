namespace EasyRedmineTool.Core.Services;

using EasyRedmineTool.Core.Models.TimeEntries;

using System.Globalization;

internal sealed class TimeEntryFormDataCache
{
    private readonly object _sync = new();
    private readonly Dictionary<string, IReadOnlyList<TimeEntryActivityDto>> _activities = new();
    private readonly Dictionary<string, Lazy<Task<IReadOnlyList<TimeEntryActivityDto>>>> _activitiesInflight = new();
    private long _generation;

    public async Task<IReadOnlyList<TimeEntryActivityDto>> GetOrLoadActivitiesAsync(
        string cacheKey,
        Func<CancellationToken, Task<IReadOnlyList<TimeEntryActivityDto>>> loader,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Lazy<Task<IReadOnlyList<TimeEntryActivityDto>>> load;
        lock (_sync)
        {
            if (_activities.TryGetValue(cacheKey, out var cachedActivities))
            {
                return cachedActivities;
            }

            if (!_activitiesInflight.TryGetValue(cacheKey, out load!))
            {
                var generation = _generation;
                load = new(() => LoadActivitiesAsync(cacheKey, generation, loader));
                _activitiesInflight.Add(cacheKey, load);
            }
        }

        // A waiter owns its cancellation; the shared operation belongs to the cache.
        return await load.Value.WaitAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<TimeEntryActivityDto>> LoadActivitiesAsync(
        string cacheKey,
        long generation,
        Func<CancellationToken, Task<IReadOnlyList<TimeEntryActivityDto>>> loader)
    {
        try
        {
            var loaded = await loader(CancellationToken.None);
            lock (_sync)
            {
                if (_generation == generation)
                {
                    _activities[cacheKey] = loaded;
                }
            }

            return loaded;
        }
        finally
        {
            lock (_sync)
            {
                if (_generation == generation)
                {
                    _activitiesInflight.Remove(cacheKey);
                }
            }
        }
    }

    public void Invalidate()
    {
        lock (_sync)
        {
            _generation++;
            _activities.Clear();
            _activitiesInflight.Clear();
        }
    }

    public static string BuildActivitiesKey(string baseUrl, string apiKey, int? issueId, int? projectId) =>
        $"{baseUrl}|{apiKey}|issue:{issueId?.ToString(CultureInfo.InvariantCulture) ?? "-"}|project:{projectId?.ToString(CultureInfo.InvariantCulture) ?? "-"}";
}
