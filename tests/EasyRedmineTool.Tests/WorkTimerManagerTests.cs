namespace EasyRedmineTool.Tests;

using EasyRedmineTool.Core.TimeTracking;

public class WorkTimerManagerTests
{
    private static readonly DateTime BaseTime = new(2026, 6, 6, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Start_on_second_issue_pauses_first_issue()
    {
        var manager = new WorkTimerManager();

        manager.Start(100, BaseTime.Date, BaseTime);
        manager.Start(200, BaseTime.Date, BaseTime.AddMinutes(15));

        Assert.Equal(200, manager.RunningIssueId);

        var firstSession = manager.GetSession(100);
        Assert.NotNull(firstSession);
        Assert.False(firstSession.IsRunning);
        Assert.Equal(TimeSpan.FromMinutes(15), firstSession.GetElapsed(BaseTime.AddMinutes(15)));
    }

    [Fact]
    public void Stop_returns_elapsed_time_and_clears_session()
    {
        var manager = new WorkTimerManager();
        manager.Start(100, BaseTime.Date, BaseTime);

        var elapsed = manager.Stop(100, BaseTime.AddMinutes(45));

        Assert.Equal(TimeSpan.FromMinutes(45), elapsed);
        Assert.Null(manager.GetSession(100));
        Assert.Null(manager.RunningIssueId);
    }

    [Fact]
    public void Resume_after_pause_continues_accumulating_time()
    {
        var manager = new WorkTimerManager();
        manager.Start(100, BaseTime.Date, BaseTime);
        manager.Pause(100, BaseTime.AddMinutes(20));
        manager.Start(100, BaseTime.Date, BaseTime.AddMinutes(30));

        var elapsed = manager.Stop(100, BaseTime.AddMinutes(45));

        Assert.Equal(TimeSpan.FromMinutes(35), elapsed);
    }
}
