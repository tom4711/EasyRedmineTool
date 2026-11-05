namespace EasyRedmineTool.Core.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using EasyRedmineTool.Core;
using EasyRedmineTool.Core.Api;
using EasyRedmineTool.Core.Configuration;
using EasyRedmineTool.Core.Models.TimeEntries;
using EasyRedmineTool.Core.Models.Tickets;
using EasyRedmineTool.Core.Services.Interfaces;

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

public partial class SeriesBookingViewModel : ViewModelBase, IDisposable
{
    private readonly IAppSettingsService _appSettingsService;
    private readonly ITimeEntryService _timeEntryService;
    private CancellationTokenSource? _ticketDetailsCts;
    private CancellationTokenSource? _ticketFilterCts;
    private CancellationTokenSource? _previewCts;
    private string _connectionIdentity;
    private int _connectionVersion;
    private string _confirmedConnectionIdentity = string.Empty;
    private int _confirmedConnectionVersion;
    private int _previewVersion;
    private int _previewLoadVersion;
    private List<DateTime> _confirmedDates = [];
    private IssueDto? _confirmedTicket;
    private TimeEntryActivityDto? _confirmedActivity;
    private double _confirmedHours;
    private string _confirmedComments = string.Empty;
    private IReadOnlyList<TimeEntryCustomFieldValue> _confirmedCustomFields = [];

    [ObservableProperty]
    private IssueDto? selectedTicket;

    [ObservableProperty]
    private string ticketFilterText = string.Empty;

    [ObservableProperty]
    private bool showFavoritesOnly = true;

    [ObservableProperty]
    private string ticketListScopeSummary = string.Empty;

    [ObservableProperty]
    private DateTimeOffset? fromDate = DateTime.Today.AddDays(-20);

    [ObservableProperty]
    private DateTimeOffset? toDate = DateTime.Today;

    [ObservableProperty]
    private bool includeMonday = true;

    [ObservableProperty]
    private bool includeTuesday = true;

    [ObservableProperty]
    private bool includeWednesday = true;

    [ObservableProperty]
    private bool includeThursday = true;

    [ObservableProperty]
    private bool includeFriday = true;

    [ObservableProperty]
    private bool includeSaturday;

    [ObservableProperty]
    private bool includeSunday;

    [ObservableProperty]
    private string hours = "2";

    [ObservableProperty]
    private TimeEntryActivityDto? selectedActivity;

    [ObservableProperty]
    private string comments = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanBook))]
    private bool isLoadingPreview;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanBook))]
    private bool isBooking;

    [ObservableProperty]
    private bool hasPreview;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWarningMessage))]
    private string warningMessage = string.Empty;

    [ObservableProperty]
    private bool showBookingConfirmation;

    [ObservableProperty]
    private string bookingConfirmationText = string.Empty;

    public SeriesBookingViewModel(IAppSettingsService appSettingsService, ITimeEntryService timeEntryService)
    {
        _appSettingsService = appSettingsService;
        _timeEntryService = timeEntryService;
        _connectionIdentity = CurrentConnectionIdentity();
    }

    public ObservableCollection<IssueDto> Tickets { get; } = [];

    public ObservableCollection<TimeEntryActivityDto> Activities { get; } = [];

    public ObservableCollection<TimeEntryCustomFieldRowViewModel> CustomFields { get; } = [];

    public ObservableCollection<SeriesBookingPreviewRowViewModel> PreviewRows { get; } = [];

    public bool HasCustomFields => CustomFields.Count > 0;

    public bool HasWarningMessage => !string.IsNullOrWhiteSpace(WarningMessage);

    public string TicketSearchPlaceholder => ShowFavoritesOnly
        ? "In Favoriten suchen (Nr., Betreff, Projekt) …"
        : "In allen Tickets suchen (Nr., Betreff, Projekt) …";

    public string PreviewSummary
    {
        get
        {
            var selected = PreviewRows.Where(row => row.IsSelected).ToList();
            if (selected.Count == 0)
            {
                return "Keine Tage ausgewählt.";
            }

            var totalHours = selected.Sum(row => row.Hours);
            var conflictCount = PreviewRows.Count(row => row.HasConflict);
            var summary = $"{selected.Count} Einträge, {totalHours.ToString("0.##", CultureInfo.InvariantCulture)} Stunden gesamt";
            if (conflictCount > 0)
            {
                summary += $" ({conflictCount} Konflikt(e))";
            }

            return summary;
        }
    }

    public bool CanBook => HasPreview && PreviewRows.Any(row => row.IsSelected) && !IsLoadingPreview && !IsBooking;

    public string BookingConfirmationActionLabel => _confirmedDates.Count == 1
        ? "1 Eintrag buchen"
        : $"{_confirmedDates.Count} Einträge buchen";

    public void PrepareView()
    {
        if (_connectionIdentity != CurrentConnectionIdentity())
        {
            ResetForConnectionChange();
        }

        ReloadTickets();
        StatusMessage = string.Empty;

        if (SelectedTicket is not null)
        {
            _ = LoadTicketDetailsAsync();
        }
    }

    public void ResetForConnectionChange()
    {
        _connectionVersion++;
        _connectionIdentity = CurrentConnectionIdentity();
        CancelTicketDetailsLoad();
        CancelTicketFilterLoad();
        ClearPreview();
        Tickets.Clear();
        SelectedTicket = null;
        Activities.Clear();
        CustomFields.Clear();
        SelectedActivity = null;
        _confirmedDates.Clear();
        _confirmedTicket = null;
        _confirmedActivity = null;
        _confirmedCustomFields = [];
        _confirmedComments = string.Empty;
        _confirmedHours = 0;
        _confirmedConnectionIdentity = string.Empty;
        BookingConfirmationText = string.Empty;
        Comments = string.Empty;
        TicketFilterText = string.Empty;
        CancelTicketFilterLoad();
        TicketListScopeSummary = string.Empty;
        StatusMessage = string.Empty;
        WarningMessage = string.Empty;
        OnPropertyChanged(nameof(HasCustomFields));
    }

    private string CurrentConnectionIdentity()
    {
        var settings = _appSettingsService.Load();
        return ConnectionIdentity.Get(settings.BaseUrl, settings.ApiKey);
    }

    private bool IsCurrentConnection(string identity, int version) =>
        version == _connectionVersion && identity == _connectionIdentity && identity == CurrentConnectionIdentity();

    partial void OnSelectedTicketChanged(IssueDto? value)
    {
        ClearPreview();
        _ = LoadTicketDetailsAsync();
    }

    partial void OnFromDateChanged(DateTimeOffset? value) => ClearPreview();
    partial void OnToDateChanged(DateTimeOffset? value) => ClearPreview();
    partial void OnHoursChanged(string value) => ClearPreview();
    partial void OnSelectedActivityChanged(TimeEntryActivityDto? value) => InvalidateBookingConfirmation();
    partial void OnCommentsChanged(string value) => InvalidateBookingConfirmation();

    partial void OnShowFavoritesOnlyChanged(bool value)
    {
        OnPropertyChanged(nameof(TicketSearchPlaceholder));
        ReloadTickets();
    }

    partial void OnTicketFilterTextChanged(string value)
    {
        _ = ReloadTicketsDebouncedAsync();
    }

    private async Task ReloadTicketsDebouncedAsync()
    {
        _ticketFilterCts?.Cancel();
        _ticketFilterCts?.Dispose();
        _ticketFilterCts = new CancellationTokenSource();
        var token = _ticketFilterCts.Token;

        try
        {
            await Task.Delay(250, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!token.IsCancellationRequested)
        {
            ReloadTickets();
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
    }

    [RelayCommand]
    private void ShowAllTicketsView()
    {
        if (!ShowFavoritesOnly)
        {
            return;
        }

        ShowFavoritesOnly = false;
    }

    partial void OnIncludeMondayChanged(bool value) => ClearPreview();
    partial void OnIncludeTuesdayChanged(bool value) => ClearPreview();
    partial void OnIncludeWednesdayChanged(bool value) => ClearPreview();
    partial void OnIncludeThursdayChanged(bool value) => ClearPreview();
    partial void OnIncludeFridayChanged(bool value) => ClearPreview();
    partial void OnIncludeSaturdayChanged(bool value) => ClearPreview();
    partial void OnIncludeSundayChanged(bool value) => ClearPreview();

    [RelayCommand]
    private void ApplyLastWeekPreset() => ApplyPreset(days: 7);

    [RelayCommand]
    private void ApplyLast2WeeksPreset() => ApplyPreset(days: 14);

    [RelayCommand]
    private void ApplyLast3WeeksPreset() => ApplyPreset(days: 21);

    [RelayCommand]
    private void SelectWeekdaysOnly()
    {
        IncludeMonday = true;
        IncludeTuesday = true;
        IncludeWednesday = true;
        IncludeThursday = true;
        IncludeFriday = true;
        IncludeSaturday = false;
        IncludeSunday = false;
    }

    [RelayCommand]
    private void SelectAllWeekdays()
    {
        IncludeMonday = true;
        IncludeTuesday = true;
        IncludeWednesday = true;
        IncludeThursday = true;
        IncludeFriday = true;
        IncludeSaturday = true;
        IncludeSunday = true;
    }

    private void ApplyPreset(int days)
    {
        ToDate = DateTime.Today;
        FromDate = DateTime.Today.AddDays(-days);
        SelectWeekdaysOnly();
        StatusMessage = $"Zeitraum: letzte {days} Tage. Vorschau aktualisieren.";
    }

    [RelayCommand]
    private async Task RefreshPreviewAsync()
    {
        await RefreshPreviewCoreAsync();
    }

    private async Task<bool> RefreshPreviewCoreAsync(
        IReadOnlySet<DateTime>? bookedDates = null,
        HashSet<DateTime>? selectedDates = null)
    {
        var connectionIdentity = _connectionIdentity;
        var connectionVersion = _connectionVersion;
        if (!IsCurrentConnection(connectionIdentity, connectionVersion) || IsLoadingPreview || IsBooking)
        {
            return false;
        }

        if (SelectedTicket is null)
        {
            StatusMessage = "Bitte ein Ticket auswählen.";
            return false;
        }

        if (!FromDate.HasValue || !ToDate.HasValue)
        {
            StatusMessage = "Bitte Von- und Bis-Datum auswählen.";
            return false;
        }

        if (ToDate.Value.Date < FromDate.Value.Date)
        {
            StatusMessage = "Das Enddatum muss am oder nach dem Startdatum liegen.";
            return false;
        }

        if (!TimeEntryHours.TryParsePositive(Hours, out var parsedHours))
        {
            StatusMessage = "Bitte gültige Stunden eingeben.";
            return false;
        }

        if (SelectedActivity is null)
        {
            StatusMessage = "Bitte eine Aktivität auswählen.";
            return false;
        }

        var settings = _appSettingsService.Load();
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            StatusMessage = "API-Key fehlt. Bitte zuerst Einstellungen speichern.";
            return false;
        }

        var selectedWeekdays = GetSelectedWeekdays();
        if (selectedWeekdays.Count == 0)
        {
            StatusMessage = "Bitte mindestens einen Wochentag auswählen.";
            return false;
        }

        var customFieldError = TimeEntryCustomFieldSupport.Validate(CustomFields);
        if (customFieldError is not null)
        {
            StatusMessage = customFieldError;
            return false;
        }

        var plannedDates = SeriesBookingPlanner.GenerateDates(FromDate.Value.Date, ToDate.Value.Date, selectedWeekdays);
        if (plannedDates.Count == 0)
        {
            HasPreview = false;
            PreviewRows.Clear();
            NotifyPreviewChanged();
            StatusMessage = "Im gewählten Zeitraum gibt es keine passenden Tage.";
            return false;
        }

        _previewLoadVersion++;
        var previewCts = new CancellationTokenSource();
        _previewCts = previewCts;
        var previewToken = previewCts.Token;
        try
        {
            IsLoadingPreview = true;
            StatusMessage = "Bestehende Einträge werden geprüft …";
            var previewVersion = _previewVersion;
            var ticketId = SelectedTicket.Id;

            var loadResult = await _timeEntryService.GetMyTimeEntriesAsync(
                settings.BaseUrl,
                settings.ApiKey,
                FromDate.Value.Date,
                ToDate.Value.Date,
                previewToken);

            if (!IsCurrentConnection(connectionIdentity, connectionVersion) || previewToken.IsCancellationRequested)
            {
                return false;
            }

            if (previewVersion != _previewVersion)
            {
                StatusMessage = "Eingaben geändert. Bitte Vorschau erneut laden.";
                return false;
            }

            if (!loadResult.Success)
            {
                StatusMessage = loadResult.Message;
                return false;
            }

            var conflictDates = SeriesBookingPlanner.FindConflictDates(ticketId, loadResult.Entries).ToHashSet();
            if (bookedDates is not null)
            {
                conflictDates.UnionWith(bookedDates);
            }

            PreviewRows.Clear();
            foreach (var date in plannedDates)
            {
                var row = new SeriesBookingPreviewRowViewModel(
                    date,
                    parsedHours,
                    conflictDates.Contains(date.Date),
                    NotifyPreviewChanged);
                if (selectedDates is not null)
                {
                    row.IsSelected = selectedDates.Contains(date.Date) && !row.HasConflict;
                }

                PreviewRows.Add(row);
            }

            HasPreview = true;
            NotifyPreviewChanged();
            StatusMessage = $"Vorschau: {PreviewRows.Count} Tage, {PreviewRows.Count(row => row.HasConflict)} Konflikt(e).";
            return true;
        }
        catch (OperationCanceledException) when (previewToken.IsCancellationRequested)
        {
            return false;
        }
        finally
        {
            if (ReferenceEquals(_previewCts, previewCts))
            {
                _previewCts = null;
                IsLoadingPreview = false;
            }
            previewCts.Dispose();
        }
    }

    [RelayCommand]
    private void SelectAllPreviewRows()
    {
        foreach (var row in PreviewRows)
        {
            row.IsSelected = true;
        }

        NotifyPreviewChanged();
    }

    [RelayCommand]
    private void DeselectAllPreviewRows()
    {
        foreach (var row in PreviewRows)
        {
            row.IsSelected = false;
        }

        NotifyPreviewChanged();
    }

    [RelayCommand]
    private void DeselectConflictRows()
    {
        foreach (var row in PreviewRows.Where(row => row.HasConflict))
        {
            row.IsSelected = false;
        }

        NotifyPreviewChanged();
    }

    [RelayCommand]
    private void RequestBookSeries()
    {
        if (!IsCurrentConnection(_connectionIdentity, _connectionVersion) || !CanBook || SelectedTicket is null || SelectedActivity is null)
        {
            return;
        }

        var customFieldError = TimeEntryCustomFieldSupport.Validate(CustomFields);
        if (customFieldError is not null)
        {
            StatusMessage = customFieldError;
            return;
        }

        var selectedRows = PreviewRows.Where(row => row.IsSelected).OrderBy(row => row.Date).ToList();
        var selectedCount = selectedRows.Count;
        if (selectedCount == 0)
        {
            StatusMessage = "Keine Tage zum Buchen ausgewählt.";
            return;
        }

        if (!TimeEntryHours.TryParsePositive(Hours, out var parsedHours))
        {
            StatusMessage = "Bitte gültige Stunden eingeben.";
            return;
        }

        _confirmedConnectionIdentity = _connectionIdentity;
        _confirmedConnectionVersion = _connectionVersion;
        _confirmedDates = selectedRows.Select(row => row.Date).ToList();
        _confirmedTicket = SelectedTicket;
        _confirmedActivity = SelectedActivity;
        _confirmedHours = parsedHours;
        _confirmedComments = Comments;
        _confirmedCustomFields = TimeEntryCustomFieldSupport.BuildValues(CustomFields).ToList();
        OnPropertyChanged(nameof(BookingConfirmationActionLabel));
        var hoursPerEntry = parsedHours.ToString("G", CultureInfo.InvariantCulture);
        var totalHours = (selectedCount * parsedHours).ToString("G", CultureInfo.InvariantCulture);
        var selectedConflictCount = selectedRows.Count(row => row.HasConflict);
        var conflictSummary = selectedConflictCount == 0
            ? "Keine Konflikttage ausgewählt."
            : $"{selectedConflictCount} Konflikttage bewusst ausgewählt.";
        BookingConfirmationText =
            $"{selectedCount} Einträge × {hoursPerEntry} h = {totalHours} h\n" +
            $"Zeitraum: {selectedRows[0].DateLabel} – {selectedRows[^1].DateLabel}\n" +
            $"Ticket #{SelectedTicket.Id}: {SelectedTicket.Subject}\n" +
            conflictSummary;
        ShowBookingConfirmation = true;
    }

    [RelayCommand]
    private void CancelBookSeriesConfirmation() => ShowBookingConfirmation = false;

    [RelayCommand]
    private async Task ConfirmBookSeriesAsync()
    {
        if (!IsCurrentConnection(_confirmedConnectionIdentity, _confirmedConnectionVersion) ||
            !ShowBookingConfirmation || IsBooking || _confirmedTicket is null || _confirmedActivity is null)
        {
            return;
        }

        var confirmedDates = _confirmedDates.ToHashSet();
        var selectedRows = PreviewRows
            .Where(row => row.IsSelected && confirmedDates.Contains(row.Date))
            .OrderBy(row => row.Date)
            .ToList();
        if (selectedRows.Count == 0 || selectedRows.Count != _confirmedDates.Count)
        {
            ShowBookingConfirmation = false;
            StatusMessage = selectedRows.Count == 0
                ? "Keine Tage zum Buchen ausgewählt."
                : "Die Vorschau hat sich geändert. Bitte erneut bestätigen.";
            return;
        }

        var settings = _appSettingsService.Load();
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            ShowBookingConfirmation = false;
            StatusMessage = "API-Key fehlt. Bitte zuerst Einstellungen speichern.";
            return;
        }

        var customFieldError = TimeEntryCustomFieldSupport.Validate(CustomFields);
        if (customFieldError is not null)
        {
            ShowBookingConfirmation = false;
            StatusMessage = customFieldError;
            return;
        }

        var bookingIdentity = _confirmedConnectionIdentity;
        var bookingVersion = _confirmedConnectionVersion;
        var bookingPreviewVersion = _previewVersion;
        var bookingPreviewLoadVersion = _previewLoadVersion;

        bool IsBookingPreviewCurrent() =>
            IsCurrentConnection(bookingIdentity, bookingVersion) &&
            bookingPreviewVersion == _previewVersion && bookingPreviewLoadVersion == _previewLoadVersion;

        var bookingTicket = _confirmedTicket;
        var bookingActivity = _confirmedActivity;
        var parsedHours = _confirmedHours;
        var comments = _confirmedComments;
        var customFieldValues = _confirmedCustomFields;
        ShowBookingConfirmation = false;

        var successCount = 0;
        var failureMessages = new List<string>();
        var bookedDates = new HashSet<DateTime>();
        WarningMessage = string.Empty;

        try
        {
            IsBooking = true;

            for (var index = 0; index < selectedRows.Count; index++)
            {
                if (!IsCurrentConnection(bookingIdentity, bookingVersion))
                {
                    return;
                }

                var row = selectedRows[index];
                StatusMessage = $"Buche {index + 1} von {selectedRows.Count} ({row.DateLabel}) …";

                var result = await _timeEntryService.CreateTimeEntryAsync(
                    settings.BaseUrl,
                    settings.ApiKey,
                    new TimeEntryCreateRequest
                    {
                        IssueId = bookingTicket.Id,
                        Hours = parsedHours,
                        SpentOn = RedmineDates.FormatSpentOn(row.Date),
                        ActivityId = bookingActivity.Id,
                        Comments = comments,
                        CustomFields = customFieldValues.ToList()
                    });

                if (!IsCurrentConnection(bookingIdentity, bookingVersion))
                {
                    return;
                }

                if (result.Success)
                {
                    successCount++;
                    bookedDates.Add(row.Date.Date);
                    row.MarkBooked();
                }
                else
                {
                    failureMessages.Add($"{row.DateLabel}: {result.Message}");
                }
            }
        }
        finally
        {
            IsBooking = false;
        }

        if (!IsBookingPreviewCurrent())
        {
            return;
        }

        var bookingSummary = failureMessages.Count == 0
            ? $"{successCount} Zeiteinträge wurden erstellt."
            : $"{successCount} erstellt, {failureMessages.Count} fehlgeschlagen. {failureMessages[0]}";
        StatusMessage = bookingSummary;

        if (successCount > 0)
        {
            try
            {
                TimeEntryCustomFieldSupport.SaveDefaults(_appSettingsService, CustomFields);
                await _appSettingsService.FlushAsync();
            }
            catch (Exception ex)
            {
                if (!IsBookingPreviewCurrent())
                {
                    return;
                }
                AddWarning($"Buchung erfolgreich; lokale Vorgaben konnten nicht gespeichert werden: {ex.Message}");
            }

            if (!IsBookingPreviewCurrent())
            {
                return;
            }

            var remainingDates = PreviewRows.Where(row => row.IsSelected).Select(row => row.Date.Date).ToHashSet();
            try
            {
                var refreshTask = RefreshPreviewCoreAsync(bookedDates, remainingDates);
                bookingPreviewLoadVersion = _previewLoadVersion;
                var refreshed = await refreshTask;
                if (!IsBookingPreviewCurrent())
                {
                    return;
                }
                if (!refreshed)
                {
                    AddWarning($"Vorschau konnte nach der Buchung nicht aktualisiert werden: {StatusMessage}");
                }
            }
            catch (Exception ex)
            {
                if (!IsBookingPreviewCurrent())
                {
                    return;
                }
                AddWarning($"Vorschau konnte nach der Buchung nicht aktualisiert werden: {ex.Message}");
            }
        }

        if (!IsBookingPreviewCurrent())
        {
            return;
        }

        if (failureMessages.Count == 0)
        {
            Comments = string.Empty;
        }

        StatusMessage = bookingSummary;
        NotifyPreviewChanged();
    }

    private void AddWarning(string message) =>
        WarningMessage = string.IsNullOrEmpty(WarningMessage) ? message : $"{WarningMessage} {message}";

    private void ReloadTickets()
    {
        var settings = _appSettingsService.Load();
        var favoriteIds = settings.FavoriteTicketIds.ToHashSet();

        var scopedTickets = (ShowFavoritesOnly
                ? settings.CachedTickets.Where(ticket => favoriteIds.Contains(ticket.Id))
                : settings.CachedTickets)
            .ToList();

        var orderedTickets = scopedTickets
            .OrderByDescending(ticket => favoriteIds.Contains(ticket.Id))
            .ThenBy(ticket => ticket.Id)
            .ToList();

        var filteredTickets = orderedTickets
            .Where(ticket => MatchesTicketFilter(ticket))
            .ToList();

        Tickets.Clear();
        foreach (var ticket in filteredTickets)
        {
            Tickets.Add(ticket);
        }

        if (SelectedTicket is null || Tickets.All(ticket => ticket.Id != SelectedTicket.Id))
        {
            SelectedTicket = Tickets.FirstOrDefault(ticket => favoriteIds.Contains(ticket.Id))
                ?? Tickets.FirstOrDefault();
        }

        UpdateTicketScopeSummary(total: orderedTickets.Count, filtered: Tickets.Count);
    }

    private bool MatchesTicketFilter(IssueDto ticket)
    {
        if (string.IsNullOrWhiteSpace(TicketFilterText))
        {
            return true;
        }

        var query = TicketFilterText.Trim();
        return ticket.Id.ToString(CultureInfo.InvariantCulture).Contains(query, StringComparison.OrdinalIgnoreCase)
            || (ticket.Subject?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
            || (ticket.Project?.Name?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private void UpdateTicketScopeSummary(int total, int filtered)
    {
        var hasSearch = !string.IsNullOrWhiteSpace(TicketFilterText);
        var scopeLabel = ShowFavoritesOnly ? "Favoriten" : "Tickets";

        TicketListScopeSummary = total switch
        {
            0 => ShowFavoritesOnly ? "Keine Favoriten" : "Keine Tickets geladen",
            _ when hasSearch && filtered != total => $"{filtered} von {total} {scopeLabel}",
            1 => ShowFavoritesOnly ? "1 Favorit" : "1 Ticket",
            _ => $"{total} {scopeLabel}"
        };
    }

    private async Task LoadTicketDetailsAsync()
    {
        var ticket = SelectedTicket;
        CancelTicketDetailsLoad();

        Activities.Clear();
        CustomFields.Clear();
        SelectedActivity = null;
        OnPropertyChanged(nameof(HasCustomFields));
        ClearPreview();

        if (ticket is null)
        {
            return;
        }

        var loadToken = BeginTicketDetailsLoad();
        var ticketId = ticket.Id;
        var connectionIdentity = _connectionIdentity;
        var connectionVersion = _connectionVersion;

        var settings = _appSettingsService.Load();
        if (!IsCurrentConnection(connectionIdentity, connectionVersion) || string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            return;
        }

        try
        {
            var loadedActivities = await _timeEntryService.GetActivitiesAsync(
                settings.BaseUrl,
                settings.ApiKey,
                ticketId,
                ticket.Project?.Id,
                loadToken);

            if (ShouldIgnoreTicketDetailsResult(ticketId, connectionIdentity, connectionVersion, loadToken))
            {
                return;
            }

            Activities.Clear();
            foreach (var activity in loadedActivities.OrderBy(activity => activity.Name))
            {
                Activities.Add(activity);
            }

            SelectedActivity = Activities.FirstOrDefault();

            var recentValues = await _timeEntryService.GetRecentCustomFieldValuesAsync(
                settings.BaseUrl,
                settings.ApiKey,
                ticketId,
                ticket.Project?.Id,
                loadToken);

            if (ShouldIgnoreTicketDetailsResult(ticketId, connectionIdentity, connectionVersion, loadToken))
            {
                return;
            }

            CustomFields.Clear();
            foreach (var row in TimeEntryCustomFieldSupport.CreateRows(recentValues, settings))
            {
                CustomFields.Add(row);
            }

            OnPropertyChanged(nameof(HasCustomFields));
            InvalidateBookingConfirmation();
        }
        catch (OperationCanceledException) when (loadToken.IsCancellationRequested)
        {
        }
        catch (RedmineApiException ex)
        {
            if (IsCurrentConnection(connectionIdentity, connectionVersion))
            {
                StatusMessage = ex.Message;
            }
        }
    }

    private bool ShouldIgnoreTicketDetailsResult(int ticketId, string connectionIdentity, int connectionVersion, CancellationToken loadToken) =>
        loadToken.IsCancellationRequested || SelectedTicket?.Id != ticketId || !IsCurrentConnection(connectionIdentity, connectionVersion);

    private void CancelTicketDetailsLoad()
    {
        _ticketDetailsCts?.Cancel();
        _ticketDetailsCts?.Dispose();
        _ticketDetailsCts = null;
    }

    private void CancelTicketFilterLoad()
    {
        _ticketFilterCts?.Cancel();
        _ticketFilterCts?.Dispose();
        _ticketFilterCts = null;
    }

    private CancellationToken BeginTicketDetailsLoad()
    {
        _ticketDetailsCts = new CancellationTokenSource();
        return _ticketDetailsCts.Token;
    }

    public void Dispose()
    {
        CancelTicketDetailsLoad();
        CancelTicketFilterLoad();
        CancelPreviewLoad();
        GC.SuppressFinalize(this);
    }

    private IReadOnlySet<DayOfWeek> GetSelectedWeekdays() =>
        SeriesBookingWeekdays.FromFlags(
            IncludeMonday,
            IncludeTuesday,
            IncludeWednesday,
            IncludeThursday,
            IncludeFriday,
            IncludeSaturday,
            IncludeSunday);

    private void CancelPreviewLoad()
    {
        var pending = _previewCts;
        _previewCts = null;
        pending?.Cancel();
        pending?.Dispose();
        IsLoadingPreview = false;
    }

    private void ClearPreview()
    {
        CancelPreviewLoad();
        _previewVersion++;
        HasPreview = false;
        PreviewRows.Clear();
        InvalidateBookingConfirmation();
        NotifyPreviewChanged();
    }

    private void NotifyPreviewChanged()
    {
        InvalidateBookingConfirmation();
        OnPropertyChanged(nameof(PreviewSummary));
        OnPropertyChanged(nameof(CanBook));
    }

    private void InvalidateBookingConfirmation()
    {
        if (ShowBookingConfirmation)
        {
            ShowBookingConfirmation = false;
        }
    }
}
