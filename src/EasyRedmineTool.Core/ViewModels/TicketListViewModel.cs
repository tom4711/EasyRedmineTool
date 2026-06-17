namespace EasyRedmineTool.Core.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using EasyRedmineTool.Core;
using EasyRedmineTool.Core.Api;
using EasyRedmineTool.Core.Configuration;
using EasyRedmineTool.Core.Models.Tickets;
using EasyRedmineTool.Core.Services;
using EasyRedmineTool.Core.Services.Interfaces;

using System.Collections.ObjectModel;

public partial class TicketListViewModel : ViewModelBase, IDisposable
{
    private readonly ITicketService _ticketService;
    private readonly IAppSettingsService _appSettingsService;
    private readonly HashSet<int> _favoriteTicketIds = [];
    private CancellationTokenSource? _operationCts;
    private TicketStatusFilterKind _pendingStatusFilterKind = TicketStatusFilterKind.Open;
    private int? _pendingStatusId;
    private string? _pendingStatusName;
    private bool _isRestoringStatusFilter;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string ticketIdToAdd = string.Empty;

    [ObservableProperty]
    private TicketListItemViewModel? selectedTicket;

    [ObservableProperty]
    private TicketFilterOption<TicketAssigneeFilter>? selectedAssigneeFilter;

    [ObservableProperty]
    private TicketFilterOption<TicketStatusFilterSelection>? selectedStatusFilter;

    [ObservableProperty]
    private bool includeTimeEntryTickets;

    [ObservableProperty]
    private int timeEntryLookbackMonths = TicketService.DefaultTimeEntryLookbackMonths;

    public ObservableCollection<TicketListItemViewModel> Tickets { get; } = [];

    public ObservableCollection<TicketFilterOption<TicketStatusFilterSelection>> StatusFilterOptions { get; } = [];

    public IReadOnlyList<TicketFilterOption<TicketAssigneeFilter>> AssigneeFilterOptions { get; } =
    [
        new("Mir zugewiesen", TicketAssigneeFilter.Me),
        new("Nicht zugewiesen", TicketAssigneeFilter.Unassigned),
        new("Alle", TicketAssigneeFilter.All)
    ];

    public TicketListViewModel(ITicketService ticketService, IAppSettingsService appSettingsService)
    {
        _ticketService = ticketService;
        _appSettingsService = appSettingsService;

        ReloadSettings();
        _ = ReloadStatusFilterOptionsAsync();
    }

    public bool IsTimeEntryLookback3Months => TimeEntryLookbackMonths == 3;

    public bool IsTimeEntryLookback6Months => TimeEntryLookbackMonths == 6;

    public bool IsTimeEntryLookback9Months => TimeEntryLookbackMonths == 9;

    public bool IsTimeEntryLookback12Months => TimeEntryLookbackMonths == 12;

    partial void OnSelectedAssigneeFilterChanged(TicketFilterOption<TicketAssigneeFilter>? value) =>
        PersistFilterSettings();

    partial void OnSelectedStatusFilterChanged(TicketFilterOption<TicketStatusFilterSelection>? value)
    {
        if (_isRestoringStatusFilter || value is null)
        {
            return;
        }

        PersistFilterSettings();
    }

    partial void OnIncludeTimeEntryTicketsChanged(bool value) =>
        PersistFilterSettings();

    partial void OnTimeEntryLookbackMonthsChanged(int value)
    {
        var normalized = TicketService.NormalizeTimeEntryLookbackMonths(value);
        if (normalized != value)
        {
            TimeEntryLookbackMonths = normalized;
            return;
        }

        OnPropertyChanged(nameof(IsTimeEntryLookback3Months));
        OnPropertyChanged(nameof(IsTimeEntryLookback6Months));
        OnPropertyChanged(nameof(IsTimeEntryLookback9Months));
        OnPropertyChanged(nameof(IsTimeEntryLookback12Months));
        PersistFilterSettings();
    }

    [RelayCommand]
    private void SelectTimeEntryLookback3Months() => TimeEntryLookbackMonths = 3;

    [RelayCommand]
    private void SelectTimeEntryLookback6Months() => TimeEntryLookbackMonths = 6;

    [RelayCommand]
    private void SelectTimeEntryLookback9Months() => TimeEntryLookbackMonths = 9;

    [RelayCommand]
    private void SelectTimeEntryLookback12Months() => TimeEntryLookbackMonths = 12;

    public void ReloadSettings()
    {
        var settings = _appSettingsService.Load();

        _favoriteTicketIds.Clear();
        foreach (var id in settings.FavoriteTicketIds)
        {
            _favoriteTicketIds.Add(id);
        }

        SelectedAssigneeFilter = AssigneeFilterOptions.First(option => option.Value == settings.TicketLoadAssigneeFilter);
        _pendingStatusFilterKind = settings.TicketLoadStatusFilterKind;
        _pendingStatusId = settings.TicketLoadStatusId;
        _pendingStatusName = settings.TicketLoadStatusName;
        IncludeTimeEntryTickets = settings.TicketLoadIncludeTimeEntryTickets;
        TimeEntryLookbackMonths = TicketService.NormalizeTimeEntryLookbackMonths(
            settings.TicketLoadTimeEntryLookbackMonths);
        OnPropertyChanged(nameof(IsTimeEntryLookback3Months));
        OnPropertyChanged(nameof(IsTimeEntryLookback6Months));
        OnPropertyChanged(nameof(IsTimeEntryLookback9Months));
        OnPropertyChanged(nameof(IsTimeEntryLookback12Months));
        RestoreSelectedStatusFilter();

        Tickets.Clear();
        var lastLoadedIds = settings.LastLoadedTicketIds.Count > 0
            ? settings.LastLoadedTicketIds.ToHashSet()
            : settings.CachedTickets.Select(ticket => ticket.Id).ToHashSet();

        foreach (var ticket in settings.CachedTickets.Where(ticket => lastLoadedIds.Contains(ticket.Id)))
        {
            Tickets.Add(CreateTicketItem(ticket));
        }
    }

    [RelayCommand]
    private async Task LoadTicketsAsync()
    {
        var operationCts = BeginOperation();
        var cancellationToken = operationCts.Token;

        try
        {
            IsBusy = true;
            StatusMessage = "Tickets werden geladen ...";
            Tickets.Clear();

            SyncPendingFromSelection();
            var filter = BuildCurrentFilter();
            var (baseUrl, apiKey) = LoadCredentials();
            var result = await _ticketService.GetTicketsForListAsync(baseUrl, apiKey, filter, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            foreach (var ticket in result.Tickets)
            {
                Tickets.Add(CreateTicketItem(ticket));
            }

            PersistCurrentState();
            RestoreSelectedStatusFilter(SelectedStatusFilter?.Value);
            StatusMessage = BuildLoadStatusMessage(result, filter, SelectedStatusFilter?.Value);
        }
        catch (OperationCanceledException)
        {
            // A newer operation superseded this request.
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fehler beim Laden: {ex.Message}";
        }
        finally
        {
            CompleteOperation(operationCts);
        }
    }

    [RelayCommand]
    private async Task AddTicketByIdAsync()
    {
        if (IsBusy)
            return;

        if (!int.TryParse(TicketIdToAdd, out var ticketId) || ticketId <= 0)
        {
            StatusMessage = "Bitte eine gültige Ticket-ID eingeben.";
            return;
        }

        if (Tickets.Any(t => t.Ticket.Id == ticketId))
        {
            StatusMessage = "Ticket ist bereits in der Liste vorhanden.";
            return;
        }

        var operationCts = BeginOperation();
        var cancellationToken = operationCts.Token;

        try
        {
            IsBusy = true;
            var (baseUrl, apiKey) = LoadCredentials();
            var ticket = await _ticketService.GetIssueByIdAsync(baseUrl, apiKey, ticketId, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (ticket is null)
            {
                StatusMessage = "Ticket wurde nicht gefunden.";
                return;
            }

            Tickets.Add(CreateTicketItem(ticket));
            TicketIdToAdd = string.Empty;
            PersistCurrentState();
            StatusMessage = $"Ticket #{ticket.Id} wurde hinzugefügt.";
        }
        catch (OperationCanceledException)
        {
            // A newer operation superseded this request.
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fehler beim Hinzufügen: {ex.Message}";
        }
        finally
        {
            CompleteOperation(operationCts);
        }
    }

    [RelayCommand]
    private void ToggleFavoriteForTicket(TicketListItemViewModel? ticketItem)
    {
        if (ticketItem is null)
        {
            return;
        }

        ToggleFavorite(ticketItem);
    }

    [RelayCommand]
    private void OpenTicketInBrowser(TicketListItemViewModel? ticketItem)
    {
        if (ticketItem is null)
        {
            return;
        }

        var (baseUrl, _) = LoadCredentials();
        RedmineLinks.OpenIssueInBrowser(baseUrl, ticketItem.Ticket.Id);
    }

    private async Task ReloadStatusFilterOptionsAsync(CancellationToken cancellationToken = default)
    {
        var (baseUrl, apiKey) = LoadCredentials();
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey))
        {
            ApplyDefaultStatusFilterOptions();
            RestoreSelectedStatusFilter();
            return;
        }

        try
        {
            var statuses = await _ticketService.GetIssueStatusesAsync(baseUrl, apiKey, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            RebuildStatusFilterOptions(statuses);
            RestoreSelectedStatusFilter();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (RedmineApiException ex)
        {
            ApplyDefaultStatusFilterOptions();
            RestoreSelectedStatusFilter();
            StatusMessage = ex.Message;
        }
        catch
        {
            ApplyDefaultStatusFilterOptions();
            RestoreSelectedStatusFilter();
        }
    }

    private void RebuildStatusFilterOptions(IReadOnlyList<StatusDto> statuses)
    {
        var preservedSelection = SelectedStatusFilter?.Value;
        PopulateStatusFilterOptions(statuses);
        RestoreSelectedStatusFilter(preservedSelection);
    }

    private void ApplyDefaultStatusFilterOptions()
    {
        var preservedSelection = SelectedStatusFilter?.Value;
        PopulateStatusFilterOptions([]);
        RestoreSelectedStatusFilter(preservedSelection);
    }

    private void PopulateStatusFilterOptions(IReadOnlyList<StatusDto> statuses)
    {
        _isRestoringStatusFilter = true;
        try
        {
            StatusFilterOptions.Clear();
            StatusFilterOptions.Add(new TicketFilterOption<TicketStatusFilterSelection>(
                TicketStatusFilterSelection.All.Label,
                TicketStatusFilterSelection.All));
            StatusFilterOptions.Add(new TicketFilterOption<TicketStatusFilterSelection>(
                TicketStatusFilterSelection.Open.Label,
                TicketStatusFilterSelection.Open));
            StatusFilterOptions.Add(new TicketFilterOption<TicketStatusFilterSelection>(
                TicketStatusFilterSelection.Closed.Label,
                TicketStatusFilterSelection.Closed));

            foreach (var status in statuses.OrderBy(status => status.Is_Closed).ThenBy(status => status.Name))
            {
                StatusFilterOptions.Add(new TicketFilterOption<TicketStatusFilterSelection>(
                    status.Name,
                    TicketStatusFilterSelection.FromStatus(status)));
            }
        }
        finally
        {
            _isRestoringStatusFilter = false;
        }
    }

    private void RestoreSelectedStatusFilter(TicketStatusFilterSelection? preservedSelection = null)
    {
        var selection = preservedSelection
            ?? TicketStatusFilterSelection.TryCreate(_pendingStatusFilterKind, _pendingStatusId, _pendingStatusName);

        _isRestoringStatusFilter = true;
        try
        {
            SelectedStatusFilter = selection is null
                ? FindStatusFilterOption(TicketStatusFilterSelection.Open)
                : FindStatusFilterOption(selection)
                  ?? FindStatusFilterOption(TicketStatusFilterSelection.Open)
                  ?? StatusFilterOptions.FirstOrDefault();
        }
        finally
        {
            _isRestoringStatusFilter = false;
        }
    }

    private TicketFilterOption<TicketStatusFilterSelection>? FindStatusFilterOption(TicketStatusFilterSelection selection) =>
        StatusFilterOptions.FirstOrDefault(option => MatchesStatusSelection(option.Value, selection));

    private static bool MatchesStatusSelection(
        TicketStatusFilterSelection left,
        TicketStatusFilterSelection right) =>
        left.Kind == right.Kind
        && left.StatusId == right.StatusId;

    private void SyncPendingFromSelection()
    {
        var selection = SelectedStatusFilter?.Value;
        if (selection is null)
        {
            return;
        }

        _pendingStatusFilterKind = selection.Kind;
        _pendingStatusId = selection.StatusId;
        _pendingStatusName = selection.Kind == TicketStatusFilterKind.Specific ? selection.Label : null;
    }

    private void ToggleFavorite(TicketListItemViewModel ticketItem)
    {
        if (ticketItem.IsFavorite)
        {
            _favoriteTicketIds.Remove(ticketItem.Ticket.Id);
            ticketItem.IsFavorite = false;
            StatusMessage = $"Ticket #{ticketItem.Ticket.Id} aus Favoriten entfernt.";
        }
        else
        {
            _favoriteTicketIds.Add(ticketItem.Ticket.Id);
            ticketItem.IsFavorite = true;
            StatusMessage = $"Ticket #{ticketItem.Ticket.Id} als Favorit markiert.";
        }

        PersistCurrentState();
    }

    private TicketListItemViewModel CreateTicketItem(IssueDto ticket) =>
        new(ticket, _favoriteTicketIds.Contains(ticket.Id));

    private TicketLoadFilter BuildCurrentFilter()
    {
        var statusSelection = SelectedStatusFilter?.Value
            ?? TicketStatusFilterSelection.TryCreate(_pendingStatusFilterKind, _pendingStatusId, _pendingStatusName)
            ?? TicketStatusFilterSelection.Open;
        return new TicketLoadFilter
        {
            Assignee = SelectedAssigneeFilter?.Value ?? TicketAssigneeFilter.Me,
            StatusKind = statusSelection.Kind,
            StatusId = statusSelection.StatusId,
            IncludeTimeEntryTickets = IncludeTimeEntryTickets,
            TimeEntryLookbackMonths = TimeEntryLookbackMonths
        };
    }

    private static string BuildLoadStatusMessage(
        TicketListLoadResult result,
        TicketLoadFilter filter,
        TicketStatusFilterSelection? statusSelection)
    {
        var filterSummary = BuildFilterSummary(filter, statusSelection);
        if (!filter.IncludeTimeEntryTickets)
        {
            return $"{result.Tickets.Count} Ticket(s) geladen (nur Zuweisung/Status{filterSummary}).";
        }

        if (result.TimeEntryTicketCount == 0)
        {
            return $"{result.Tickets.Count} Ticket(s) geladen ({result.OpenTicketCount} aus Filter{filterSummary}).";
        }

        return $"{result.Tickets.Count} Ticket(s) geladen ({result.OpenTicketCount} aus Filter, {result.TimeEntryTicketCount} mit Zeiteinträgen in den letzten {filter.TimeEntryLookbackMonths} Monaten{filterSummary}).";
    }

    private static string BuildFilterSummary(
        TicketLoadFilter filter,
        TicketStatusFilterSelection? statusSelection)
    {
        var parts = new List<string>
        {
            DescribeAssigneeFilter(filter.Assignee),
            DescribeStatusFilter(filter, statusSelection)
        };

        if (filter.IncludeTimeEntryTickets)
        {
            parts.Add($"Zeiteinträge der letzten {filter.TimeEntryLookbackMonths} Monate");
        }

        return $"; Filter: {string.Join(", ", parts)}";
    }

    private static string DescribeAssigneeFilter(TicketAssigneeFilter filter) =>
        filter switch
        {
            TicketAssigneeFilter.Me => "mir zugewiesen",
            TicketAssigneeFilter.Unassigned => "nicht zugewiesen",
            TicketAssigneeFilter.All => "alle Zuweisungen",
            _ => filter.ToString()
        };

    private static string DescribeStatusFilter(
        TicketLoadFilter filter,
        TicketStatusFilterSelection? statusSelection) =>
        filter.StatusKind switch
        {
            TicketStatusFilterKind.Open => "alle offenen Status",
            TicketStatusFilterKind.Closed => "alle geschlossenen Status",
            TicketStatusFilterKind.Specific => statusSelection?.Label ?? $"Status #{filter.StatusId}",
            _ => "alle Status"
        };

    private (string BaseUrl, string ApiKey) LoadCredentials()
    {
        var settings = _appSettingsService.Load();
        return (settings.BaseUrl, settings.ApiKey);
    }

    private void PersistCurrentState()
    {
        _appSettingsService.Update(settings =>
        {
            var loadedTickets = Tickets.Select(ticket => ticket.Ticket).ToList();
            settings.CachedTickets = TicketCacheMerger.Merge(
                loadedTickets,
                settings.CachedTickets,
                _favoriteTicketIds);
            settings.FavoriteTicketIds = _favoriteTicketIds.ToList();
            settings.LastLoadedTicketIds = loadedTickets.Select(ticket => ticket.Id).ToList();
        });
    }

    private void PersistFilterSettings()
    {
        var statusSelection = SelectedStatusFilter?.Value ?? TicketStatusFilterSelection.Open;
        _pendingStatusFilterKind = statusSelection.Kind;
        _pendingStatusId = statusSelection.StatusId;
        _pendingStatusName = statusSelection.Kind == TicketStatusFilterKind.Specific ? statusSelection.Label : null;

        _appSettingsService.Update(settings =>
        {
            settings.TicketLoadAssigneeFilter = SelectedAssigneeFilter?.Value ?? TicketAssigneeFilter.Me;
            settings.TicketLoadStatusFilterKind = statusSelection.Kind;
            settings.TicketLoadStatusId = statusSelection.StatusId;
            settings.TicketLoadStatusName = _pendingStatusName;
            settings.TicketLoadIncludeTimeEntryTickets = IncludeTimeEntryTickets;
            settings.TicketLoadTimeEntryLookbackMonths = TimeEntryLookbackMonths;
        });
    }

    private CancellationTokenSource BeginOperation()
    {
        CancelOperation();
        var operationCts = new CancellationTokenSource();
        _operationCts = operationCts;
        return operationCts;
    }

    private void CompleteOperation(CancellationTokenSource operationCts)
    {
        if (_operationCts != operationCts)
        {
            return;
        }

        IsBusy = false;
        _operationCts = null;
    }

    private void CancelOperation()
    {
        _operationCts?.Cancel();
        _operationCts?.Dispose();
        _operationCts = null;
    }

    public void Dispose()
    {
        CancelOperation();
        GC.SuppressFinalize(this);
    }
}
