namespace EasyRedmineTool.Core.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;

using EasyRedmineTool.Core.Models.TimeEntries;

using System.Collections.ObjectModel;
using System.Globalization;

public partial class WeeklyHoursRowViewModel : ViewModelBase
{
    public WeeklyHoursRowViewModel(int year, int week, double hours, IEnumerable<WeeklyTicketHoursDto> tickets)
    {
        Year = year;
        Week = week;
        Hours = hours;

        foreach (var ticket in tickets)
        {
            Tickets.Add(ticket);
        }
    }

    public int Year { get; }

    public int Week { get; }

    public double Hours { get; }

    public string Label => $"KW {Week} / {Year}";

    public string HoursDisplay =>
        $"{Hours.ToString("0.##", CultureInfo.GetCultureInfo("de-DE"))} h";

    public bool HasTickets => Tickets.Count > 0;

    public ObservableCollection<WeeklyTicketHoursDto> Tickets { get; } = [];

    [ObservableProperty]
    private bool isExpanded;
}
