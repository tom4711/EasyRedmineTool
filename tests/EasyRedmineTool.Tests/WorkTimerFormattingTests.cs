namespace EasyRedmineTool.Tests;

using EasyRedmineTool.Core.TimeTracking;

public class WorkTimerFormattingTests
{
    [Fact]
    public void FormatElapsed_uses_hh_mm_ss()
    {
        Assert.Equal("1:05:09", WorkTimerFormatting.FormatElapsed(TimeSpan.FromHours(1) + TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(9)));
    }

    [Fact]
    public void ToBookableHours_enforces_minimum()
    {
        Assert.Equal(0.01, WorkTimerFormatting.ToBookableHours(TimeSpan.FromSeconds(10)));
        Assert.Equal(1.25, WorkTimerFormatting.ToBookableHours(TimeSpan.FromHours(1.25)));
    }
}
