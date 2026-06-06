namespace EasyRedmineTool.Core.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string favoriteFilterText = string.Empty;

    [ObservableProperty]
    private double todayBookedHours;

    [ObservableProperty]
    private bool canRepeatLastEntry;

    [ObservableProperty]
    private string lastEntrySummaryLabel = string.Empty;

    public ObservableCollection<FavoriteTimeEntryRowViewModel> FavoriteRows { get; } = [];
    public ObservableCollection<FavoriteTimeEntryRowViewModel> FilteredFavoriteRows { get; } = [];

    public TimeEntriesViewModel(IAppSettingsService appSettingsService, ITimeEntryService timeEntryService)
    {
        _appSettingsService = appSettingsService;
        _timeEntryService = timeEntryService;

        ReloadFavorites();
        _ = ReloadTodayBookedHoursAsync();
    }

    public string TodayBookedHoursDisplay =>
        $"{TodayBookedHours.ToString("0.##", CultureInfo.GetCultureInfo("de-DE"))} h";

    partial void OnTodayBookedHoursChanged(double value)
    {
        OnPropertyChanged(nameof(TodayBookedHoursDisplay));
    }

    internal void SetStatusMessage(string message)
    {
        StatusMessage = message;
    }

    internal async Task HandleEntryCreatedAsync(
        FavoriteTimeEntryRowViewModel row,
        DateTime spentOn,
        TimeEntryActivityDto activity)
    {
        SaveLastTimeEntryTemplate(row, activity);
        UpdateCachedTicketLastTimeEntry(row.Ticket.Id, spentOn);
        ReloadFavorites();
        await ReloadTodayBookedHoursAsync();
    }

    [RelayCommand]
    public void ReloadFavorites()
    {
        var settings = _appSettingsService.Load();
        var previousState = FavoriteRows.ToDictionary(
            row => row.Ticket.Id,
            row => (row.Hours, row.Comments, row.SpentOn, SelectedActivityId: row.SelectedActivity?.Id));

        var favorites = settings.CachedTickets
            .Where(t => settings.FavoriteTicketIds.Contains(t.Id))
            .OrderByDescending(t => t.LastTimeEntryOn.HasValue)
            .ThenByDescending(t => t.LastTimeEntryOn)
            .ThenBy(t => t.Id)
            .ToList();

        FavoriteRows.Clear();
        foreach (var ticket in favorites)
        {
            var row = new FavoriteTimeEntryRowViewModel(this, ticket, _appSettingsService, _timeEntryService);
            int? selectedActivityId = null;
            if (previousState.TryGetValue(ticket.Id, out var state))
            {
                row.Hours = state.Hours;
                row.Comments = state.Comments;
                row.SpentOn = state.SpentOn ?? DateTime.Today;
                selectedActivityId = state.SelectedActivityId;
            }

            FavoriteRows.Add(row);
            _ = RestoreAndLoadRowAsync(row, selectedActivityId);
        }

        ApplyFavoriteFilter();
        UpdateCanRepeatLastEntry();

        StatusMessage = FavoriteRows.Count == 0
            ? "Keine Favoriten vorhanden. Bitte in der Ticketliste Favoriten markieren."
            : string.Empty;
    }

    private async Task RestoreAndLoadRowAsync(FavoriteTimeEntryRowViewModel row, int? selectedActivityId)
    {
        await row.LoadActivitiesAsync();

        if (selectedActivityId.HasValue)
        {
            row.SelectedActivity = row.Activities.FirstOrDefault(a => a.Id == selectedActivityId.Value)
                ?? row.SelectedActivity;
        }
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

    private void ApplyFavoriteFilter()
    {
        FilteredFavoriteRows.Clear();
        foreach (var row in FavoriteRows.Where(row => MatchesFavoriteFilter(row.Ticket)))
        {
            FilteredFavoriteRows.Add(row);
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

    [RelayCommand(CanExecute = nameof(CanRepeatLastEntry))]
    private async Task RepeatLastEntryAsync()
    {
        var settings = _appSettingsService.Load();
        if (!settings.LastTimeEntryIssueId.HasValue || !settings.LastTimeEntryActivityId.HasValue)
        {
            return;
        }

        var row = FavoriteRows.FirstOrDefault(r => r.Ticket.Id == settings.LastTimeEntryIssueId.Value);
        if (row is null)
        {
            StatusMessage = "Letztes Ticket ist kein Favorit mehr.";
            UpdateCanRepeatLastEntry();
            return;
        }

        await row.LoadActivitiesAsync();

        var hours = string.IsNullOrWhiteSpace(settings.LastTimeEntryHours) ? "1" : settings.LastTimeEntryHours;
        row.ApplyTemplate(
            settings.LastTimeEntryActivityId.Value,
            hours,
            DateTime.Today,
            settings.LastTimeEntryActivityName);

        await row.CreateTimeEntryCommand.ExecuteAsync(null);
    }

    private void SaveLastTimeEntryTemplate(FavoriteTimeEntryRowViewModel row, TimeEntryActivityDto activity)
    {
        _appSettingsService.Update(settings =>
        {
            settings.LastTimeEntryIssueId = row.Ticket.Id;
            settings.LastTimeEntryActivityId = activity.Id;
            settings.LastTimeEntryHours = row.Hours;
            settings.LastTimeEntryActivityName = activity.Name;
        });
        UpdateCanRepeatLastEntry();
    }

    private void UpdateCachedTicketLastTimeEntry(int issueId, DateTime spentOn)
    {
        _appSettingsService.Update(settings =>
        {
            var ticket = settings.CachedTickets.FirstOrDefault(t => t.Id == issueId);
            if (ticket is null)
            {
                return;
            }

            if (ticket.LastTimeEntryOn is null || spentOn >= ticket.LastTimeEntryOn)
            {
                ticket.LastTimeEntryOn = spentOn;
            }
        });
    }

    private void UpdateCanRepeatLastEntry()
    {
        var settings = _appSettingsService.Load();
        CanRepeatLastEntry = settings.LastTimeEntryIssueId.HasValue
            && settings.LastTimeEntryActivityId.HasValue
            && FavoriteRows.Any(r => r.Ticket.Id == settings.LastTimeEntryIssueId.Value);

        LastEntrySummaryLabel = CanRepeatLastEntry
            ? BuildLastEntrySummary(settings)
            : string.Empty;

        RepeatLastEntryCommand.NotifyCanExecuteChanged();
    }

    private string BuildLastEntrySummary(AppSettings settings)
    {
        var ticket = FavoriteRows.FirstOrDefault(r => r.Ticket.Id == settings.LastTimeEntryIssueId)?.Ticket
            ?? _appSettingsService.Load().CachedTickets.FirstOrDefault(t => t.Id == settings.LastTimeEntryIssueId);

        var ticketPart = ticket is not null
            ? $"#{ticket.Id} {Truncate(ticket.Subject, 48)}"
            : $"#{settings.LastTimeEntryIssueId}";

        var hours = string.IsNullOrWhiteSpace(settings.LastTimeEntryHours) ? "1" : settings.LastTimeEntryHours;
        var activity = string.IsNullOrWhiteSpace(settings.LastTimeEntryActivityName)
            ? "Aktivität"
            : settings.LastTimeEntryActivityName;

        return $"{ticketPart} · {hours} h · {activity}";
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..(maxLength - 1)] + "…";
    }
}
