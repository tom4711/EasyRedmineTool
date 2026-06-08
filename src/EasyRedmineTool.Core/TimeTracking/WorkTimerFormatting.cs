namespace EasyRedmineTool.Core.TimeTracking;

using System.Globalization;

public static class WorkTimerFormatting
{
    public const double MinimumBookableHours = 0.01;

    public static string FormatElapsed(TimeSpan elapsed)
    {
        var totalHours = (int)Math.Floor(elapsed.TotalHours);
        return $"{totalHours}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
    }

    public static double ToBookableHours(TimeSpan elapsed)
    {
        if (elapsed <= TimeSpan.Zero)
        {
            return 0;
        }

        return Math.Max(MinimumBookableHours, Math.Round(elapsed.TotalHours, 2, MidpointRounding.AwayFromZero));
    }

    public static string FormatHoursForInput(double hours) =>
        hours.ToString("0.##", CultureInfo.InvariantCulture);
}
