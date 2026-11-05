namespace EasyRedmineTool.Tests;

using EasyRedmineTool.Core.Models.TimeEntries;
using EasyRedmineTool.Core.Services;

public class TimeEntryFormDataCacheTests
{
    [Fact]
    public async Task GetOrLoadActivitiesAsync_completes_shared_load_even_when_first_caller_is_cancelled()
    {
        var cache = new TimeEntryFormDataCache();
        var loadCount = 0;
        var gate = new TaskCompletionSource();

        Task<IReadOnlyList<TimeEntryActivityDto>> Loader(CancellationToken cancellationToken)
        {
            loadCount++;
            return WaitForGateAsync(gate);
        }

        using var cancelled = new CancellationTokenSource();
        var firstLoad = cache.GetOrLoadActivitiesAsync("activities", Loader, cancelled.Token);
        var secondLoad = cache.GetOrLoadActivitiesAsync("activities", Loader, CancellationToken.None);

        cancelled.Cancel();
        gate.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => firstLoad);
        var activities = await secondLoad;

        Assert.Equal(1, loadCount);
        Assert.Single(activities);
        Assert.Equal("Dev", activities[0].Name);
    }

    [Fact]
    public async Task Invalidate_detaches_old_load_and_prevents_stale_publication()
    {
        var cache = new TimeEntryFormDataCache();
        var oldGate = new TaskCompletionSource<IReadOnlyList<TimeEntryActivityDto>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var freshGate = new TaskCompletionSource<IReadOnlyList<TimeEntryActivityDto>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;
        Task<IReadOnlyList<TimeEntryActivityDto>> Loader(CancellationToken _) => ++count == 1 ? oldGate.Task : freshGate.Task;
        var oldLoad = cache.GetOrLoadActivitiesAsync("key", Loader, default);
        cache.Invalidate();
        var newLoad = cache.GetOrLoadActivitiesAsync("key", Loader, default);
        var loadsAfterInvalidate = count;
        oldGate.SetResult([new() { Id = 1 }]);
        await oldLoad;
        var otherNewReader = cache.GetOrLoadActivitiesAsync("key", Loader, default);
        freshGate.SetResult([new() { Id = 2 }]);
        Assert.Equal(2, loadsAfterInvalidate);
        Assert.Equal(2, (await newLoad)[0].Id);
        Assert.Equal(2, (await otherNewReader)[0].Id);
        Assert.Equal(2, (await cache.GetOrLoadActivitiesAsync("key", Loader, default))[0].Id);
        Assert.Equal(2, count);
    }

    private static async Task<IReadOnlyList<TimeEntryActivityDto>> WaitForGateAsync(
        TaskCompletionSource gate)
    {
        await gate.Task;
        return [new TimeEntryActivityDto { Id = 1, Name = "Dev" }];
    }
}
