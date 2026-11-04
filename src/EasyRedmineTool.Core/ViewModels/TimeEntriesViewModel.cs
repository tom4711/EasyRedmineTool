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
    private readonly Dictionary<int, FavoriteTimeEntryRowViewModel> _rows = [];
    private readonly Dictionary<int, TodayTimeEntryRowViewModel> _todayRows = [];
    private readonly HashSet<int> _deletedEntryIds = [];
    private readonly SemaphoreSlim _formLoadSlots = new(4);
    private string? _connectionIdentity;
    internal int ConnectionVersion { get; private set; }

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWarningMessage))]
    private string warningMessage = string.Empty;
    public bool HasWarningMessage => !string.IsNullOrWhiteSpace(WarningMessage);
    internal void SetWarningMessage(string message) => WarningMessage = message;

    [ObservableProperty]
    private string favoriteFilterText = string.Empty;

    [ObservableProperty]
    private bool showFavoritesOnly = true;

    [ObservableProperty]
    private string listScopeSummary = string.Empty;

    [ObservableProperty]
    private double todayBookedHours;

    [ObservableProperty]
    private bool isTodayOverviewExpanded;

    [ObservableProperty]
    private int? focusedIssueId;

    public ObservableCollection<FavoriteTimeEntryRowViewModel> FavoriteRows { get; } = [];
    public ObservableCollection<FavoriteTimeEntryRowViewModel> FilteredFavoriteRows { get; } = [];
    public ObservableCollection<TodayTimeEntryRowViewModel> TodayTimeEntries { get; } = [];

    public TimeEntriesViewModel(IAppSettingsService appSettingsService, ITimeEntryService timeEntryService)
    {
        _appSettingsService = appSettingsService;
        _timeEntryService = timeEntryService;
        var settings = _appSettingsService.Load();
        _connectionIdentity = ConnectionIdentity.Get(settings.BaseUrl, settings.ApiKey);
    }

    public string TodayBookedHoursDisplay =>
        $"{TodayBookedHours.ToString("0.##", CultureInfo.GetCultureInfo("de-DE"))} h";

    public bool HasTodayTimeEntries => TodayTimeEntries.Count > 0;

    public string TodayEntriesSummary => TodayTimeEntries.Count switch
    {
        0 => "Keine Einträge",
        1 => "1 Eintrag",
        _ => $"{TodayTimeEntries.Count} Einträge"
    };

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

    internal void EnsureTodayOverviewExpanded()
    {
        IsTodayOverviewExpanded = true;
    }

    internal async Task HandleEntryCreatedAsync(
        FavoriteTimeEntryRowViewModel row,
        DateTime spentOn)
    {
        if (!row.IsCurrentConnection) return;
        try
        {
            UpdateCachedTicketLastTimeEntry(row.Ticket.Id, spentOn);
            await _appSettingsService.FlushAsync();
        }
        catch (Exception)
        {
            row.AddWarning("Die Buchung ist gespeichert; der lokale Ticket-Cache konnte nicht aktualisiert werden.");
        }
        if (!row.IsCurrentConnection) return;
        foreach (var favoriteRow in FavoriteRows.Where(item => item.Ticket.Id == row.Ticket.Id))
        {
            if (favoriteRow.Ticket.LastTimeEntryOn is null || spentOn >= favoriteRow.Ticket.LastTimeEntryOn)
            {
                favoriteRow.Ticket.LastTimeEntryOn = spentOn;
            }
        }

        var orderedRows = FavoriteRows
            .OrderByDescending(item => item.Ticket.LastTimeEntryOn.HasValue)
            .ThenByDescending(item => item.Ticket.LastTimeEntryOn)
            .ThenBy(item => item.Ticket.Id)
            .ToList();
        for (var index = 0; index < orderedRows.Count; index++)
        {
            FavoriteRows.Move(FavoriteRows.IndexOf(orderedRows[index]), index);
        }

        ApplyFavoriteFilter();
        var commitMessage = StatusMessage;
        if (!await RefreshTodayCoreAsync() && row.IsCurrentConnection)
        {
            StatusMessage = commitMessage;
            row.AddWarning("Die Buchung ist gespeichert; die Tagesübersicht konnte nicht aktualisiert werden.");
        }
    }

    [RelayCommand]
    public void ReloadFavorites()
    {
        var settings = _appSettingsService.Load();
        var identity = SynchronizeConnection(settings);
        _rowLoadCts ??= new CancellationTokenSource();
        var favorites = settings.FavoriteTicketIds.ToHashSet();
        var tickets = (ShowFavoritesOnly
                ? settings.CachedTickets.Where(t => favorites.Contains(t.Id))
                : settings.CachedTickets)
            .OrderByDescending(t => t.LastTimeEntryOn.HasValue)
            .ThenByDescending(t => t.LastTimeEntryOn)
            .ThenBy(t => t.Id).ToList();
        FavoriteRows.Clear();
        foreach (var ticket in tickets)
        {
            if (!_rows.TryGetValue(ticket.Id, out var row))
            {
                row = new FavoriteTimeEntryRowViewModel(this, ticket, _appSettingsService, _timeEntryService, identity);
                _rows.Add(ticket.Id, row);
            }
            else row.UpdateTicket(ticket);
            FavoriteRows.Add(row);
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

    private string SynchronizeConnection(AppSettings settings)
    {
        var identity = ConnectionIdentity.Get(settings.BaseUrl, settings.ApiKey);
        if (_connectionIdentity != identity)
        {
            CancelRowLoads();
            CancelTodayHoursLoad();
            _connectionIdentity = identity;
            ConnectionVersion++;
            _rows.Clear();
            FavoriteRows.Clear();
            FilteredFavoriteRows.Clear();
            _todayRows.Clear();
            _deletedEntryIds.Clear();
            WarningMessage = string.Empty;
            TodayTimeEntries.Clear();
            TodayBookedHours = 0;
            FocusedIssueId = null;
            OnPropertyChanged(nameof(HasTodayTimeEntries));
            OnPropertyChanged(nameof(TodayEntriesSummary));
        }
        return identity;
    }

    private string GetEmptyListMessage() =>
        ShowFavoritesOnly
            ? "Keine Favoriten vorhanden. Auf „Alle“ umschalten oder in der Ticketliste Favoriten markieren."
            : "Keine Tickets in der lokalen Liste. Bitte zuerst in der Ticketliste laden.";

    internal async Task LoadRowFormAsync(FavoriteTimeEntryRowViewModel row, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _rowLoadCts?.Token ?? CancellationToken.None);
        await _formLoadSlots.WaitAsync(linked.Token);
        try
        {
            linked.Token.ThrowIfCancellationRequested();
            if (row.IsCurrentConnection) await row.LoadActivitiesAsync(linked.Token);
            linked.Token.ThrowIfCancellationRequested();
        }
        finally { _formLoadSlots.Release(); }
    }

    [RelayCommand]
    public async Task ReloadTodayBookedHoursAsync() => await RefreshTodayCoreAsync();

    internal async Task<bool> RefreshTodayCoreAsync()
    {
        var settings = _appSettingsService.Load();
        var requestIdentity = SynchronizeConnection(settings);
        CancelTodayHoursLoad();
        var cancellationToken = BeginTodayHoursLoad();

        try
        {
            if (string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                TodayBookedHours = 0;
                TodayTimeEntries.Clear();
                OnPropertyChanged(nameof(HasTodayTimeEntries));
                OnPropertyChanged(nameof(TodayEntriesSummary));
                return false;
            }

            var today = DateTime.Today;
            var result = await _timeEntryService.GetMyTimeEntriesAsync(
                settings.BaseUrl,
                settings.ApiKey,
                today,
                today,
                cancellationToken);

            if (cancellationToken.IsCancellationRequested ||
                requestIdentity != CurrentConnectionIdentity())
            {
                return false;
            }

            if (!result.Success)
            {
                StatusMessage = result.Message;
                return false;
            }

            var todayKey = RedmineDates.TodayKey();
            var todayEntries = result.Entries
                .Where(entry => !_deletedEntryIds.Contains(entry.Id) && string.Equals(entry.Spent_On, todayKey, StringComparison.Ordinal))
                .OrderByDescending(entry => entry.Hours)
                .ThenBy(entry => entry.GetIssueId())
                .ToList();

            TodayTimeEntries.Clear();
            foreach (var entry in todayEntries)
            {
                var cachedTicket = settings.CachedTickets.FirstOrDefault(ticket => ticket.Id == entry.GetIssueId());
                if (!_todayRows.TryGetValue(entry.Id, out var row))
                {
                    row = new TodayTimeEntryRowViewModel(this, entry, cachedTicket, _appSettingsService, _timeEntryService);
                    _todayRows.Add(entry.Id, row);
                }
                else row.UpdateEntry(entry, cachedTicket);
                TodayTimeEntries.Add(row);
            }

            TodayBookedHours = Math.Round(todayEntries.Sum(entry =>
                _todayRows.TryGetValue(entry.Id, out var row) && row.IsBusy ? row.BookedHours : entry.Hours), 2);
            OnPropertyChanged(nameof(HasTodayTimeEntries));
            OnPropertyChanged(nameof(TodayEntriesSummary));
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    internal void RemoveCommittedEntry(TodayTimeEntryRowViewModel row)
    {
        _deletedEntryIds.Add(row.EntryId);
        _todayRows.Remove(row.EntryId);
        TodayTimeEntries.Remove(row);
        TodayBookedHours = Math.Max(0, TodayBookedHours - row.BookedHours);
        OnPropertyChanged(nameof(HasTodayTimeEntries));
        OnPropertyChanged(nameof(TodayEntriesSummary));
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

    internal string CurrentConnectionIdentity()
    {
        var settings = _appSettingsService.Load();
        return ConnectionIdentity.Get(settings.BaseUrl, settings.ApiKey);
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

    public void Dispose()
    {
        CancelTodayHoursLoad();
        CancelRowLoads();
        GC.SuppressFinalize(this);
    }
}
