namespace EasyRedmineTool.Core;

using System.Globalization;

public static class RedmineDates
{
    public const string SpentOnFormat = "yyyy-MM-dd";

    public static bool TryParseSpentOn(string? spentOn, out DateTime date) =>
        DateTime.TryParseExact(spentOn, SpentOnFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    public static DateTime? TryParseSpentOn(string? spentOn) =>
        TryParseSpentOn(spentOn, out var date) ? date : null;

    public static string FormatSpentOn(DateTime date) =>
        date.ToString(SpentOnFormat, CultureInfo.InvariantCulture);

    public static string TodayKey() => FormatSpentOn(DateTime.Today);
}
