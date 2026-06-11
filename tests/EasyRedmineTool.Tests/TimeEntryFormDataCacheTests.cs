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
        cancelled.Cancel();

        var firstLoad = cache.GetOrLoadActivitiesAsync("activities", Loader, cancelled.Token);
        var secondLoad = cache.GetOrLoadActivitiesAsync("activities", Loader, CancellationToken.None);

        gate.SetResult();
        var activities = await secondLoad;
        await firstLoad;

        Assert.Equal(1, loadCount);
        Assert.Single(activities);
        Assert.Equal("Dev", activities[0].Name);
    }

    private static async Task<IReadOnlyList<TimeEntryActivityDto>> WaitForGateAsync(
        TaskCompletionSource gate)
    {
        await gate.Task;
        return [new TimeEntryActivityDto { Id = 1, Name = "Dev" }];
    }
}
