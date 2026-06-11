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

public partial class TimeEntriesViewModel : ViewModelBase, IDisposable
{
    private readonly IAppSettingsService _appSettingsService;
    private readonly ITimeEntryService _timeEntryService;
    private CancellationTokenSource? _todayHoursCts;
    private CancellationTokenSource? _rowLoadCts;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private string favoriteFilterText = string.Empty;

    [ObservableProperty]
    private bool showFavoritesOnly = true;

    [ObservableProperty]
    private string listScopeSummary = string.Empty;

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

    public string BookingSectionTitle => ShowFavoritesOnly ? "Favoriten buchen" : "Tickets buchen";

    public string SearchPlaceholder => ShowFavoritesOnly
        ? "In Favoriten suchen (Nr., Betreff, Projekt) …"
        : "In allen Tickets suchen (Nr., Betreff, Projekt) …";

    partial void OnTodayBookedHoursChanged(double value)
    {
        OnPropertyChanged(nameof(TodayBookedHoursDisplay));
    }

    partial void OnShowFavoritesOnlyChanged(bool value)
    {
        OnPropertyChanged(nameof(BookingSectionTitle));
        OnPropertyChanged(nameof(SearchPlaceholder));
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
        CancelRowLoads();
        var rowLoadToken = BeginRowLoads();

        var settings = _appSettingsService.Load();
        var previousState = FavoriteRows.ToDictionary(
            row => row.Ticket.Id,
            row => (row.Hours, row.Comments, row.SpentOn, SelectedActivityId: row.SelectedActivity?.Id));

        var tickets = (ShowFavoritesOnly
                ? settings.CachedTickets.Where(t => settings.FavoriteTicketIds.Contains(t.Id))
                : settings.CachedTickets)
            .OrderByDescending(t => t.LastTimeEntryOn.HasValue)
            .ThenByDescending(t => t.LastTimeEntryOn)
            .ThenBy(t => t.Id)
            .ToList();

        FavoriteRows.Clear();
        foreach (var ticket in tickets)
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
            _ = RestoreAndLoadRowAsync(row, selectedActivityId, rowLoadToken);
        }

        ApplyFavoriteFilter();

        if (FavoriteRows.Count == 0)
        {
            StatusMessage = GetEmptyListMessage();
        }
        else if (string.IsNullOrWhiteSpace(FavoriteFilterText) && FocusedIssueId is null)
        {
            StatusMessage = string.Empty;
        }
    }

    [RelayCommand]
    private void ShowFavoritesOnlyView()
    {
        if (ShowFavoritesOnly)
        {
            return;
        }

        ShowFavoritesOnly = true;
        ReloadFavorites();
    }

    [RelayCommand]
    private void ShowAllTicketsView()
    {
        if (!ShowFavoritesOnly)
        {
            return;
        }

        ShowFavoritesOnly = false;
        ReloadFavorites();
    }

    private string GetEmptyListMessage() =>
        ShowFavoritesOnly
            ? "Keine Favoriten vorhanden. Auf „Alle“ umschalten oder in der Ticketliste Favoriten markieren."
            : "Keine Tickets in der lokalen Liste. Bitte zuerst in der Ticketliste laden.";

    private async Task RestoreAndLoadRowAsync(
        FavoriteTimeEntryRowViewModel row,
        int? selectedActivityId,
        CancellationToken cancellationToken)
    {
        try
        {
            await row.LoadActivitiesAsync(cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (selectedActivityId.HasValue)
            {
                row.SelectedActivity = row.Activities.FirstOrDefault(a => a.Id == selectedActivityId.Value)
                    ?? row.SelectedActivity;
            }
        }
        catch (OperationCanceledException)
        {
            // A newer ReloadFavorites call superseded this load.
        }
    }

    [RelayCommand]
    public async Task ReloadTodayBookedHoursAsync()
    {
        CancelTodayHoursLoad();
        var cancellationToken = BeginTodayHoursLoad();

        try
        {
            var settings = _appSettingsService.Load();
            if (string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                TodayBookedHours = 0;
                return;
            }

            var today = DateTime.Today;
            var result = await _timeEntryService.GetMyTimeEntriesAsync(
                settings.BaseUrl,
                settings.ApiKey,
                today,
                today,
                cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

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
        catch (OperationCanceledException)
        {
            // A newer reload superseded this request.
        }
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
        var settings = _appSettingsService.Load();
        var ticket = settings.CachedTickets.FirstOrDefault(t => t.Id == issueId);

        if (ticket is null)
        {
            ReloadFavorites();
            FocusedIssueId = null;
            FavoriteFilterText = string.Empty;
            ApplyFavoriteFilter();
            StatusMessage = $"#{issueId} ist nicht in der lokalen Ticketliste.";
            return;
        }

        if (!settings.FavoriteTicketIds.Contains(issueId))
        {
            ShowFavoritesOnly = false;
        }

        ReloadFavorites();
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
        UpdateListScopeSummary();
    }

    private void UpdateListScopeSummary()
    {
        var total = FavoriteRows.Count;
        var filtered = FilteredFavoriteRows.Count;
        var hasSearch = !string.IsNullOrWhiteSpace(FavoriteFilterText);
        var scopeLabel = ShowFavoritesOnly ? "Favoriten" : "Tickets";

        ListScopeSummary = total switch
        {
            0 => ShowFavoritesOnly ? "Keine Favoriten" : "Keine Tickets geladen",
            _ when hasSearch && filtered != total => $"{filtered} von {total} {scopeLabel}",
            1 => ShowFavoritesOnly ? "1 Favorit" : "1 Ticket",
            _ => $"{total} {scopeLabel}"
        };
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

    private void CancelTodayHoursLoad()
    {
        _todayHoursCts?.Cancel();
        _todayHoursCts?.Dispose();
        _todayHoursCts = null;
    }

    private CancellationToken BeginTodayHoursLoad()
    {
        _todayHoursCts = new CancellationTokenSource();
        return _todayHoursCts.Token;
    }

    private void CancelRowLoads()
    {
        _rowLoadCts?.Cancel();
        _rowLoadCts?.Dispose();
        _rowLoadCts = null;
    }

    private CancellationToken BeginRowLoads()
    {
        _rowLoadCts = new CancellationTokenSource();
        return _rowLoadCts.Token;
    }

    public void Dispose()
    {
        CancelTodayHoursLoad();
        CancelRowLoads();
        GC.SuppressFinalize(this);
    }
}
