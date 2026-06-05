namespace EasyRedmineTool.Core.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using EasyRedmineTool.Core.Models.TimeEntries;
using EasyRedmineTool.Core.Models.Tickets;
using EasyRedmineTool.Core.Services.Interfaces;

using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

public partial class TimeEntriesViewModel : ViewModelBase
{
    private readonly IAppSettingsService _appSettingsService;
    private readonly ITimeEntryService _timeEntryService;

    [ObservableProperty]
    private IssueDto? selectedFavoriteTicket;

    [ObservableProperty]
    private string hours = "1";

    [ObservableProperty]
    private DateTime? spentOn = DateTime.Today;

    [ObservableProperty]
    private DateTime calendarDisplayDate = DateTime.Today;

    [ObservableProperty]
    private TimeEntryActivityDto? selectedActivity;

    [ObservableProperty]
    private string comments = string.Empty;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string currentQuarterLabel = string.Empty;

    [ObservableProperty]
    private double weeklyTotalHours;

    [ObservableProperty]
    private double maxWeeklyHours = 1;

    public ObservableCollection<IssueDto> FavoriteTickets { get; } = [];
    public ObservableCollection<TimeEntryActivityDto> Activities { get; } = [];
    public ObservableCollection<WeeklyHoursDto> WeeklyHours { get; } = [];

    public TimeEntriesViewModel(IAppSettingsService appSettingsService, ITimeEntryService timeEntryService)
    {
        _appSettingsService = appSettingsService;
        _timeEntryService = timeEntryService;

        ReloadFavorites();
        _ = ReloadActivitiesAsync();
        _ = ReloadWeeklySummaryAsync();
    }

    [RelayCommand]
    public void ReloadFavorites()
    {
        var settings = _appSettingsService.Load();

        var favorites = settings.CachedTickets
            .Where(t => settings.FavoriteTicketIds.Contains(t.Id))
            .OrderBy(t => t.Id)
            .ToList();

        FavoriteTickets.Clear();
        foreach (var ticket in favorites)
        {
            FavoriteTickets.Add(ticket);
        }

        if (SelectedFavoriteTicket is null || !FavoriteTickets.Any(t => t.Id == SelectedFavoriteTicket.Id))
        {
            SelectedFavoriteTicket = FavoriteTickets.FirstOrDefault();
        }

        StatusMessage = FavoriteTickets.Count == 0
            ? "Keine Favoriten vorhanden. Bitte in der Ticketliste Favoriten markieren."
            : $"{FavoriteTickets.Count} Favorit(en) geladen.";
    }

    [RelayCommand]
    public async Task ReloadActivitiesAsync()
    {
        var settings = _appSettingsService.Load();
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            return;
        }

        var issueId = SelectedFavoriteTicket?.Id;
        var projectId = SelectedFavoriteTicket?.Project?.Id;

        var loadedActivities = await _timeEntryService.GetActivitiesAsync(
            settings.BaseUrl,
            settings.ApiKey,
            issueId,
            projectId);

        Activities.Clear();
        foreach (var activity in loadedActivities.OrderBy(a => a.Name))
        {
            Activities.Add(activity);
        }

        if (SelectedActivity is null || Activities.All(a => a.Id != SelectedActivity.Id))
        {
            SelectedActivity = Activities.FirstOrDefault();
        }

        if (Activities.Count == 0)
        {
            StatusMessage = "Keine Aktivitäten gefunden. Bitte API-Berechtigung/Projekt prüfen.";
        }
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

        return (from, to, $"Q{quarter} {today.Year} ({from:yyyy-MM-dd} - {to:yyyy-MM-dd})");
    }

    partial void OnSelectedFavoriteTicketChanged(IssueDto? value)
    {
        _ = ReloadActivitiesAsync();
    }

    [RelayCommand]
    private void SetSpentOnToday()
    {
        SetSpentOnDate(DateTime.Today);
    }

    [RelayCommand]
    private void SetSpentOnYesterday()
    {
        SetSpentOnDate(DateTime.Today.AddDays(-1));
    }

    private void SetSpentOnDate(DateTime date)
    {
        SpentOn = date;
        CalendarDisplayDate = date;
    }

    partial void OnSpentOnChanged(DateTime? value)
    {
        if (value.HasValue)
        {
            CalendarDisplayDate = value.Value;
        }

        OnPropertyChanged(nameof(SelectedDateLabel));
    }

    public string SelectedDateLabel =>
        SpentOn?.ToString("dddd, dd.MM.yyyy", CultureInfo.GetCultureInfo("de-DE")) ?? "Kein Datum ausgewählt";

    [RelayCommand]
    private async Task CreateTimeEntryAsync()
    {
        if (IsBusy)
            return;

        if (SelectedFavoriteTicket is null)
        {
            StatusMessage = "Bitte ein Favoriten-Ticket auswählen.";
            return;
        }

        if (!double.TryParse(Hours, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedHours) || parsedHours <= 0)
        {
            StatusMessage = "Bitte gültige Stunden eingeben (z. B. 1.5).";
            return;
        }

        if (SelectedActivity is null)
        {
            StatusMessage = "Bitte eine Aktivität auswählen.";
            return;
        }

        if (SpentOn is null)
        {
            StatusMessage = "Bitte ein Datum auswählen.";
            return;
        }

        var settings = _appSettingsService.Load();
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            StatusMessage = "API-Key fehlt. Bitte zuerst Einstellungen speichern.";
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Zeiteintrag wird erstellt ...";

            var result = await _timeEntryService.CreateTimeEntryAsync(
                settings.BaseUrl,
                settings.ApiKey,
                new TimeEntryCreateRequest
                {
                    IssueId = SelectedFavoriteTicket.Id,
                    Hours = parsedHours,
                    SpentOn = SpentOn.Value.ToString("yyyy-MM-dd"),
                    ActivityId = SelectedActivity.Id,
                    Comments = Comments
                });

            StatusMessage = result.Message;
            if (result.Success)
            {
                Comments = string.Empty;
                await ReloadWeeklySummaryAsync();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
