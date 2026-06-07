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
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private string favoriteFilterText = string.Empty;

    [ObservableProperty]
    private double todayBookedHours;

    [ObservableProperty]
    private int? focusedIssueId;

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
        DateTime spentOn)
    {
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
        var result = await _timeEntryService.GetMyTimeEntriesAsync(settings.BaseUrl, settings.ApiKey, today, today);
        if (!result.Success)
        {
            StatusMessage = result.Message;
            return;
        }

        var todayKey = RedmineDates.TodayKey();
        TodayBookedHours = Math.Round(
            result.Entries
                .Where(entry => string.Equals(entry.Spent_On, todayKey, StringComparison.Ordinal))
                .Sum(entry => entry.Hours),
            2);
    }

    partial void OnFavoriteFilterTextChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            FocusedIssueId = null;
        }

        ApplyFavoriteFilter();
    }

    public void PrepareForIssue(int issueId)
    {
        ReloadFavorites();

        var settings = _appSettingsService.Load();
        if (!settings.FavoriteTicketIds.Contains(issueId))
        {
            FocusedIssueId = null;
            FavoriteFilterText = string.Empty;
            ApplyFavoriteFilter();

            var ticket = settings.CachedTickets.FirstOrDefault(t => t.Id == issueId);
            StatusMessage = ticket is not null
                ? $"#{issueId} ist kein Favorit. Bitte in der Ticketliste als Favorit markieren."
                : $"#{issueId} ist kein Favorit und nicht in der lokalen Ticketliste.";
            return;
        }

        FocusedIssueId = issueId;
        FavoriteFilterText = string.Empty;
        ApplyFavoriteFilter();
        StatusMessage = string.Empty;
    }

    public void ClearFocusedIssue()
    {
        if (FocusedIssueId is null)
        {
            return;
        }

        FocusedIssueId = null;
        UpdateRowFocusStates();
    }

    private void ApplyFavoriteFilter()
    {
        FilteredFavoriteRows.Clear();
        foreach (var row in FavoriteRows.Where(row => MatchesFavoriteFilter(row.Ticket)))
        {
            FilteredFavoriteRows.Add(row);
        }

        UpdateRowFocusStates();
    }

    private void UpdateRowFocusStates()
    {
        foreach (var row in FavoriteRows)
        {
            row.IsFocused = FocusedIssueId == row.Ticket.Id;
        }
    }

    private bool MatchesFavoriteFilter(IssueDto ticket)
    {
        if (FocusedIssueId.HasValue && ticket.Id != FocusedIssueId.Value)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(FavoriteFilterText))
        {
            return true;
        }

        var query = FavoriteFilterText.Trim();
        return ticket.Id.ToString(CultureInfo.InvariantCulture).Contains(query, StringComparison.OrdinalIgnoreCase)
            || ticket.Subject.Contains(query, StringComparison.OrdinalIgnoreCase)
            || (ticket.Project?.Name?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);
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
}
