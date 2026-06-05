namespace EasyRedmineTool.Core.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using EasyRedmineTool.Core.Models.TimeEntries;
using EasyRedmineTool.Core.Services.Interfaces;

using System.Collections.ObjectModel;
using System.Globalization;

public partial class WeeklySummaryViewModel : ViewModelBase
{
    private readonly IAppSettingsService _appSettingsService;
    private readonly ITimeEntryService _timeEntryService;

    [ObservableProperty]
    private string currentQuarterLabel = string.Empty;

    [ObservableProperty]
    private double weeklyTotalHours;

    [ObservableProperty]
    private double maxWeeklyHours = 1;

    public ObservableCollection<WeeklyHoursDto> WeeklyHours { get; } = [];

    public WeeklySummaryViewModel(IAppSettingsService appSettingsService, ITimeEntryService timeEntryService)
    {
        _appSettingsService = appSettingsService;
        _timeEntryService = timeEntryService;
    }

    [RelayCommand]
    public async Task ReloadWeeklySummaryAsync()
    {
        var settings = _appSettingsService.Load();
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            return;
        }

        var (from, to, label) = GetCurrentQuarterRange();
        CurrentQuarterLabel = label;

        var entries = await _timeEntryService.GetMyTimeEntriesAsync(settings.BaseUrl, settings.ApiKey, from, to);

        var grouped = entries
            .Where(e => DateTime.TryParseExact(e.Spent_On, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            .Select(e => new
            {
                Entry = e,
                Date = DateTime.ParseExact(e.Spent_On, "yyyy-MM-dd", CultureInfo.InvariantCulture)
            })
            .GroupBy(x => new { Year = ISOWeek.GetYear(x.Date), Week = ISOWeek.GetWeekOfYear(x.Date) })
            .Select(g => new WeeklyHoursDto
            {
                Year = g.Key.Year,
                Week = g.Key.Week,
                Hours = Math.Round(g.Sum(x => x.Entry.Hours), 2)
            })
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Week)
            .ToList();

        WeeklyHours.Clear();
        foreach (var item in grouped)
        {
            WeeklyHours.Add(item);
        }

        WeeklyTotalHours = grouped.Sum(x => x.Hours);
        MaxWeeklyHours = grouped.Count == 0 ? 1 : Math.Max(1, grouped.Max(x => x.Hours));
    }

    private static (DateTime From, DateTime To, string Label) GetCurrentQuarterRange()
    {
        var today = DateTime.Today;
        var quarter = ((today.Month - 1) / 3) + 1;
        var fromMonth = (quarter - 1) * 3 + 1;

        var from = new DateTime(today.Year, fromMonth, 1);
        var to = from.AddMonths(3).AddDays(-1);

        return (from, to, $"Q{quarter} {today.Year} ({from:yyyy-MM-dd} – {to:yyyy-MM-dd})");
    }
}
