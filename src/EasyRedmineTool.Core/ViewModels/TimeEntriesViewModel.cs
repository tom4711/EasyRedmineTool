namespace EasyRedmineTool.Core.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using EasyRedmineTool.Core;
using EasyRedmineTool.Core.Configuration;
using EasyRedmineTool.Core.Models.TimeEntries;
using EasyRedmineTool.Core.Models.Tickets;
using EasyRedmineTool.Core.Services.Interfaces;

using System.Collections.ObjectModel;
using System.Globalization;

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
    private string favoriteFilterText = string.Empty;

    [ObservableProperty]
    private double todayBookedHours;

    [ObservableProperty]
    private bool canRepeatLastEntry;

    public ObservableCollection<IssueDto> FavoriteTickets { get; } = [];
    public ObservableCollection<IssueDto> FilteredFavoriteTickets { get; } = [];
    public ObservableCollection<TimeEntryActivityDto> Activities { get; } = [];

    public TimeEntriesViewModel(IAppSettingsService appSettingsService, ITimeEntryService timeEntryService)
    {
        _appSettingsService = appSettingsService;
        _timeEntryService = timeEntryService;

        ReloadFavorites();
        _ = ReloadActivitiesAsync();
        _ = ReloadTodayBookedHoursAsync();
    }

    public string TodayBookedHoursLabel =>
        $"Heute gebucht: {TodayBookedHours.ToString("0.##", CultureInfo.GetCultureInfo("de-DE"))} h";

    public bool HasSelectedFavoriteTicket => SelectedFavoriteTicket is not null;

    [RelayCommand]
    public void ReloadFavorites()
    {
        var settings = _appSettingsService.Load();

        var favorites = settings.CachedTickets
            .Where(t => settings.FavoriteTicketIds.Contains(t.Id))
            .OrderByDescending(t => t.LastTimeEntryOn.HasValue)
            .ThenByDescending(t => t.LastTimeEntryOn)
            .ThenBy(t => t.Id)
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

        ApplyFavoriteFilter();
        UpdateCanRepeatLastEntry();

        StatusMessage = FavoriteTickets.Count == 0
            ? "Keine Favoriten vorhanden. Bitte in der Ticketliste Favoriten markieren."
            : string.Empty;
    }

    [RelayCommand]
    public async Task ReloadTodayBookedHoursAsync()
    {
        var settings = _appSettingsService.Load();
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            TodayBookedHours = 0;
            return;
        }

        var today = DateTime.Today;
        var entries = await _timeEntryService.GetMyTimeEntriesAsync(settings.BaseUrl, settings.ApiKey, today, today);
        var todayKey = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        TodayBookedHours = Math.Round(
            entries
                .Where(entry => string.Equals(entry.Spent_On, todayKey, StringComparison.Ordinal))
                .Sum(entry => entry.Hours),
            2);
    }

    partial void OnFavoriteFilterTextChanged(string value)
    {
        ApplyFavoriteFilter();
    }

    partial void OnSelectedFavoriteTicketChanged(IssueDto? value)
    {
        OnPropertyChanged(nameof(HasSelectedFavoriteTicket));
        OpenSelectedTicketInBrowserCommand.NotifyCanExecuteChanged();
        _ = ReloadActivitiesAsync();
    }

    private void ApplyFavoriteFilter()
    {
        FilteredFavoriteTickets.Clear();
        foreach (var ticket in FavoriteTickets.Where(MatchesFavoriteFilter))
        {
            FilteredFavoriteTickets.Add(ticket);
        }

        if (SelectedFavoriteTicket is not null &&
            !FilteredFavoriteTickets.Any(t => t.Id == SelectedFavoriteTicket.Id))
        {
            SelectedFavoriteTicket = FilteredFavoriteTickets.FirstOrDefault();
        }
    }

    private bool MatchesFavoriteFilter(IssueDto ticket)
    {
        if (string.IsNullOrWhiteSpace(FavoriteFilterText))
        {
            return true;
        }

        var query = FavoriteFilterText.Trim();
        return ticket.Id.ToString(CultureInfo.InvariantCulture).Contains(query, StringComparison.OrdinalIgnoreCase)
            || ticket.Subject.Contains(query, StringComparison.OrdinalIgnoreCase)
            || (ticket.Project?.Name?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);
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

        if (Activities.Count == 0 && SelectedFavoriteTicket is not null)
        {
            StatusMessage = "Keine Aktivitäten gefunden. Bitte API-Berechtigung/Projekt prüfen.";
        }
    }

    [RelayCommand(CanExecute = nameof(CanRepeatLastEntry))]
    private async Task RepeatLastEntryAsync()
    {
        var settings = _appSettingsService.Load();
        if (!settings.LastTimeEntryIssueId.HasValue || !settings.LastTimeEntryActivityId.HasValue)
        {
            return;
        }

        var ticket = FavoriteTickets.FirstOrDefault(t => t.Id == settings.LastTimeEntryIssueId.Value);
        if (ticket is null)
        {
            StatusMessage = "Letztes Ticket ist kein Favorit mehr.";
            UpdateCanRepeatLastEntry();
            return;
        }

        SelectedFavoriteTicket = ticket;
        Hours = string.IsNullOrWhiteSpace(settings.LastTimeEntryHours) ? "1" : settings.LastTimeEntryHours;
        SetSpentOnDate(DateTime.Today);

        await ReloadActivitiesAsync();

        SelectedActivity = Activities.FirstOrDefault(a => a.Id == settings.LastTimeEntryActivityId.Value)
            ?? SelectedActivity;

        StatusMessage = $"Letzter Eintrag übernommen (#{ticket.Id}, {Hours} h).";
    }

    [RelayCommand(CanExecute = nameof(HasSelectedFavoriteTicket))]
    private void OpenSelectedTicketInBrowser()
    {
        if (SelectedFavoriteTicket is null)
        {
            return;
        }

        var settings = _appSettingsService.Load();
        RedmineLinks.OpenIssueInBrowser(settings.BaseUrl, SelectedFavoriteTicket.Id);
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
            StatusMessage = "Zeiteintrag wird erstellt …";

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
                SaveLastTimeEntryTemplate();
                UpdateCachedTicketLastTimeEntry(SelectedFavoriteTicket.Id, SpentOn.Value);
                ReloadFavorites();
                await ReloadTodayBookedHoursAsync();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void SaveLastTimeEntryTemplate()
    {
        var settings = _appSettingsService.Load();
        settings.LastTimeEntryIssueId = SelectedFavoriteTicket?.Id;
        settings.LastTimeEntryActivityId = SelectedActivity?.Id;
        settings.LastTimeEntryHours = Hours;
        _appSettingsService.Save(settings);
        UpdateCanRepeatLastEntry();
    }

    private void UpdateCachedTicketLastTimeEntry(int issueId, DateTime spentOn)
    {
        var settings = _appSettingsService.Load();
        var ticket = settings.CachedTickets.FirstOrDefault(t => t.Id == issueId);
        if (ticket is null)
        {
            return;
        }

        if (ticket.LastTimeEntryOn is null || spentOn >= ticket.LastTimeEntryOn)
        {
            ticket.LastTimeEntryOn = spentOn;
            _appSettingsService.Save(settings);
        }
    }

    private void UpdateCanRepeatLastEntry()
    {
        var settings = _appSettingsService.Load();
        CanRepeatLastEntry = settings.LastTimeEntryIssueId.HasValue
            && settings.LastTimeEntryActivityId.HasValue
            && FavoriteTickets.Any(t => t.Id == settings.LastTimeEntryIssueId.Value);

        RepeatLastEntryCommand.NotifyCanExecuteChanged();
        OpenSelectedTicketInBrowserCommand.NotifyCanExecuteChanged();
    }
}
