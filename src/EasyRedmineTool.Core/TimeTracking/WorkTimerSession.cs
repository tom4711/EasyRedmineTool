namespace EasyRedmineTool.Core.TimeTracking;

public sealed class WorkTimerSession
{
    public WorkTimerSession(int issueId, DateTime spentOn)
    {
        IssueId = issueId;
        SpentOn = spentOn;
    }

    public int IssueId { get; }

    public DateTime SpentOn { get; set; }

    public TimeSpan Accumulated { get; private set; }

    public DateTime? SegmentStartedAt { get; private set; }

    public bool IsRunning => SegmentStartedAt.HasValue;

    public void Start(DateTime now)
    {
        if (IsRunning)
        {
            return;
        }

        SegmentStartedAt = now;
    }

    public void Pause(DateTime now)
    {
        if (!SegmentStartedAt.HasValue)
        {
            return;
        }

        if (now < SegmentStartedAt.Value)
        {
            throw new ArgumentException("Pause time cannot be earlier than segment start time.", nameof(now));
        }

        Accumulated += now - SegmentStartedAt.Value;
        SegmentStartedAt = null;
    }

    public TimeSpan GetElapsed(DateTime now)
    {
        if (!SegmentStartedAt.HasValue)
        {
            return Accumulated;
        }

        var delta = now - SegmentStartedAt.Value;
        if (delta < TimeSpan.Zero)
        {
            delta = TimeSpan.Zero;
        }

        return Accumulated + delta;
    }

    public bool HasTrackedTime(DateTime now) => GetElapsed(now) > TimeSpan.Zero;
}
