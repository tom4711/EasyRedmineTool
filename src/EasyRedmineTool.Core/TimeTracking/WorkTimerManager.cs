namespace EasyRedmineTool.Core.TimeTracking;

public sealed class WorkTimerManager
{
    private readonly Dictionary<int, WorkTimerSession> _sessions = [];

    public int? RunningIssueId { get; private set; }

    public IReadOnlyCollection<WorkTimerSession> Sessions => _sessions.Values;

    public WorkTimerSession? GetSession(int issueId) =>
        _sessions.GetValueOrDefault(issueId);

    public WorkTimerSession Start(int issueId, DateTime spentOn, DateTime now)
    {
        if (RunningIssueId.HasValue && RunningIssueId.Value != issueId)
        {
            Pause(RunningIssueId.Value, now);
        }

        if (!_sessions.TryGetValue(issueId, out var session))
        {
            session = new WorkTimerSession(issueId, spentOn);
            _sessions[issueId] = session;
        }
        else
        {
            session.SpentOn = spentOn;
        }

        session.Start(now);
        RunningIssueId = issueId;
        return session;
    }

    public void Pause(int issueId, DateTime now)
    {
        if (_sessions.TryGetValue(issueId, out var session))
        {
            session.Pause(now);
        }

        if (RunningIssueId == issueId)
        {
            RunningIssueId = null;
        }
    }

    public TimeSpan? Stop(int issueId, DateTime now)
    {
        if (!_sessions.TryGetValue(issueId, out var session))
        {
            return null;
        }

        session.Pause(now);
        var elapsed = session.GetElapsed(now);
        _sessions.Remove(issueId);

        if (RunningIssueId == issueId)
        {
            RunningIssueId = null;
        }

        return elapsed;
    }

    public bool IsRunning(int issueId) => RunningIssueId == issueId;
}
