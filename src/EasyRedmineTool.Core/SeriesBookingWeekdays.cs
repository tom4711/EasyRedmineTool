namespace EasyRedmineTool.Core;

public static class SeriesBookingWeekdays
{
    public static IReadOnlySet<DayOfWeek> Weekdays { get; } = new HashSet<DayOfWeek>
    {
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday
    };

    public static IReadOnlySet<DayOfWeek> All { get; } = new HashSet<DayOfWeek>(
        Enum.GetValues<DayOfWeek>());

    public static IReadOnlySet<DayOfWeek> FromFlags(
        bool monday,
        bool tuesday,
        bool wednesday,
        bool thursday,
        bool friday,
        bool saturday,
        bool sunday)
    {
        var selected = new HashSet<DayOfWeek>();
        if (monday) selected.Add(DayOfWeek.Monday);
        if (tuesday) selected.Add(DayOfWeek.Tuesday);
        if (wednesday) selected.Add(DayOfWeek.Wednesday);
        if (thursday) selected.Add(DayOfWeek.Thursday);
        if (friday) selected.Add(DayOfWeek.Friday);
        if (saturday) selected.Add(DayOfWeek.Saturday);
        if (sunday) selected.Add(DayOfWeek.Sunday);
        return selected;
    }
}
