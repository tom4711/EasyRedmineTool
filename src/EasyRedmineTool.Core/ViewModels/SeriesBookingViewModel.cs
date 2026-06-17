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
    private CancellationTokenSource? _customFieldsCts;
    private bool _suppressActivityChangeHandler;
    private CancellationTokenSource? _ticketFilterCts;

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
    private bool isLoadingPreview;

    [ObservableProperty]
    private bool isBooking;

    [ObservableProperty]
    private bool hasPreview;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool showBookingConfirmation;

    [ObservableProperty]
    private string bookingConfirmationText = string.Empty;

    public SeriesBookingViewModel(IAppSettingsService appSettingsService, ITimeEntryService timeEntryService)
    {
        _appSettingsService = appSettingsService;
        _timeEntryService = timeEntryService;
    }

    public ObservableCollection<IssueDto> Tickets { get; } = [];

    public ObservableCollection<TimeEntryActivityDto> Activities { get; } = [];

    public ObservableCollection<TimeEntryCustomFieldRowViewModel> CustomFields { get; } = [];

    public ObservableCollection<SeriesBookingPreviewRowViewModel> PreviewRows { get; } = [];

    public bool HasCustomFields => CustomFields.Count > 0;

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

    public void PrepareView()
    {
        ReloadTickets();
        StatusMessage = string.Empty;

        if (SelectedTicket is not null)
        {
            _ = LoadTicketDetailsAsync();
        }
    }

    partial void OnSelectedTicketChanged(IssueDto? value)
    {
        _ = LoadTicketDetailsAsync();
    }

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
        if (IsLoadingPreview || IsBooking)
        {
            return;
        }

        if (SelectedTicket is null)
        {
            StatusMessage = "Bitte ein Ticket auswählen.";
            return;
        }

        if (!FromDate.HasValue || !ToDate.HasValue)
        {
            StatusMessage = "Bitte Von- und Bis-Datum auswählen.";
            return;
        }

        if (ToDate.Value.Date < FromDate.Value.Date)
        {
            StatusMessage = "Das Enddatum muss am oder nach dem Startdatum liegen.";
            return;
        }

        if (!double.TryParse(Hours, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedHours) || parsedHours <= 0)
        {
            StatusMessage = "Bitte gültige Stunden eingeben.";
            return;
        }

        if (SelectedActivity is null)
        {
            StatusMessage = "Bitte eine Aktivität auswählen.";
            return;
        }

        var settings = _appSettingsService.Load();
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            StatusMessage = "API-Key fehlt. Bitte zuerst Einstellungen speichern.";
            return;
        }

        var selectedWeekdays = GetSelectedWeekdays();
        if (selectedWeekdays.Count == 0)
        {
            StatusMessage = "Bitte mindestens einen Wochentag auswählen.";
            return;
        }

        var customFieldError = TimeEntryCustomFieldSupport.Validate(CustomFields);
        if (customFieldError is not null)
        {
            StatusMessage = customFieldError;
            return;
        }

        var plannedDates = SeriesBookingPlanner.GenerateDates(FromDate.Value.Date, ToDate.Value.Date, selectedWeekdays);
        if (plannedDates.Count == 0)
        {
            HasPreview = false;
            PreviewRows.Clear();
            NotifyPreviewChanged();
            StatusMessage = "Im gewählten Zeitraum gibt es keine passenden Tage.";
            return;
        }

        try
        {
            IsLoadingPreview = true;
            StatusMessage = "Bestehende Einträge werden geprüft …";

            var loadResult = await _timeEntryService.GetMyTimeEntriesAsync(
                settings.BaseUrl,
                settings.ApiKey,
                FromDate.Value.Date,
                ToDate.Value.Date);

            if (!loadResult.Success)
            {
                StatusMessage = loadResult.Message;
                return;
            }

            var conflictDates = SeriesBookingPlanner.FindConflictDates(SelectedTicket.Id, loadResult.Entries);

            PreviewRows.Clear();
            foreach (var date in plannedDates)
            {
                PreviewRows.Add(new SeriesBookingPreviewRowViewModel(
                    date,
                    parsedHours,
                    conflictDates.Contains(date.Date),
                    NotifyPreviewChanged));
            }

            HasPreview = true;
            NotifyPreviewChanged();
            StatusMessage = $"Vorschau: {PreviewRows.Count} Tage, {PreviewRows.Count(row => row.HasConflict)} Konflikt(e).";
        }
        finally
        {
            IsLoadingPreview = false;
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
        if (!CanBook || SelectedTicket is null || SelectedActivity is null)
        {
            return;
        }

        var customFieldError = TimeEntryCustomFieldSupport.Validate(CustomFields);
        if (customFieldError is not null)
        {
            StatusMessage = customFieldError;
            return;
        }

        var selectedCount = PreviewRows.Count(row => row.IsSelected);
        if (!double.TryParse(Hours, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedHours) || parsedHours <= 0)
        {
            StatusMessage = "Bitte gültige Stunden eingeben.";
            return;
        }

        BookingConfirmationText =
            $"{selectedCount} Zeiteinträge à {parsedHours.ToString("0.##", CultureInfo.InvariantCulture)} Stunden " +
            $"für Ticket #{SelectedTicket.Id} buchen?";
        ShowBookingConfirmation = true;
    }

    [RelayCommand]
    private void CancelBookSeriesConfirmation() => ShowBookingConfirmation = false;

    [RelayCommand]
    private async Task ConfirmBookSeriesAsync()
    {
        if (IsBooking || SelectedTicket is null || SelectedActivity is null)
        {
            return;
        }

        var selectedRows = PreviewRows.Where(row => row.IsSelected).OrderBy(row => row.Date).ToList();
        if (selectedRows.Count == 0)
        {
            ShowBookingConfirmation = false;
            StatusMessage = "Keine Tage zum Buchen ausgewählt.";
            return;
        }

        if (!double.TryParse(Hours, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedHours) || parsedHours <= 0)
        {
            ShowBookingConfirmation = false;
            StatusMessage = "Bitte gültige Stunden eingeben.";
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

        var customFieldValues = TimeEntryCustomFieldSupport.BuildValues(CustomFields);
        ShowBookingConfirmation = false;

        await _timeEntryService.ResolveCustomFieldIdsAsync(settings, CustomFields);
        customFieldValues = TimeEntryCustomFieldSupport.BuildValues(CustomFields);

        try
        {
            IsBooking = true;
            var successCount = 0;
            var failureMessages = new List<string>();

            for (var index = 0; index < selectedRows.Count; index++)
            {
                var row = selectedRows[index];
                StatusMessage = $"Buche {index + 1} von {selectedRows.Count} ({row.DateLabel}) …";

                var result = await _timeEntryService.CreateTimeEntryAsync(
                    settings.BaseUrl,
                    settings.ApiKey,
                    new TimeEntryCreateRequest
                    {
                        IssueId = SelectedTicket.Id,
                        Hours = parsedHours,
                        SpentOn = RedmineDates.FormatSpentOn(row.Date),
                        ActivityId = SelectedActivity.Id,
                        Comments = Comments,
                        CustomFields = customFieldValues.ToList()
                    });

                if (result.Success)
                {
                    successCount++;
                    row.IsSelected = false;
                }
                else
                {
                    var addedFields = await _timeEntryService.TryAddMissingCustomFieldsFromBookingErrorAsync(
                        settings,
                        CustomFields,
                        result.Message);

                    if (addedFields.Count > 0)
                    {
                        OnPropertyChanged(nameof(HasCustomFields));
                        var addedField = CustomFields.FirstOrDefault(row =>
                            string.Equals(row.Name, addedFields[0], StringComparison.OrdinalIgnoreCase));
                        StatusMessage = TimeEntryCustomFieldSupport.FormatMissingFieldPrompt(
                            addedFields[0],
                            addedField?.Id ?? 0);
                        NotifyPreviewChanged();
                        return;
                    }

                    failureMessages.Add($"{row.DateLabel}: {result.Message}");
                }
            }

            if (failureMessages.Count == 0)
            {
                TimeEntryCustomFieldSupport.SaveDefaults(_appSettingsService, CustomFields);
                StatusMessage = $"{successCount} Zeiteinträge wurden erstellt.";
                Comments = string.Empty;
                await RefreshPreviewAsync();
            }
            else
            {
                StatusMessage = $"{successCount} erstellt, {failureMessages.Count} fehlgeschlagen. {failureMessages[0]}";
            }

            NotifyPreviewChanged();
        }
        finally
        {
            IsBooking = false;
        }
    }

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

        var settings = _appSettingsService.Load();
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
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

            if (ShouldIgnoreTicketDetailsResult(loadToken, ticketId))
            {
                return;
            }

            _suppressActivityChangeHandler = true;
            try
            {
                Activities.Clear();
                foreach (var activity in loadedActivities.OrderBy(activity => activity.Name))
                {
                    Activities.Add(activity);
                }

                SelectedActivity = Activities.FirstOrDefault();

                await LoadCustomFieldsAsync(loadToken, ticketId, ticket.Project?.Id);
            }
            finally
            {
                _suppressActivityChangeHandler = false;
            }
        }
        catch (OperationCanceledException) when (loadToken.IsCancellationRequested)
        {
        }
        catch (RedmineApiException ex)
        {
            StatusMessage = ex.Message;
        }
    }

    partial void OnSelectedActivityChanged(TimeEntryActivityDto? value)
    {
        if (_suppressActivityChangeHandler || SelectedTicket is null)
        {
            return;
        }

        _ = LoadCustomFieldsAsync(CancellationToken.None, SelectedTicket.Id, SelectedTicket.Project?.Id);
    }

    private async Task LoadCustomFieldsAsync(CancellationToken parentToken, int ticketId, int? projectId)
    {
        _customFieldsCts?.Cancel();
        _customFieldsCts?.Dispose();
        _customFieldsCts = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
        var loadToken = _customFieldsCts.Token;

        var settings = _appSettingsService.Load();
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            return;
        }

        CustomFields.Clear();
        OnPropertyChanged(nameof(HasCustomFields));

        if (SelectedActivity is null)
        {
            return;
        }

        if (ShouldIgnoreTicketDetailsResult(loadToken, ticketId))
        {
            return;
        }

        var customFieldRows = await _timeEntryService.GetCustomFieldRowsAsync(
            settings,
            ticketId,
            projectId,
            SelectedActivity.Id,
            cancellationToken: loadToken);

        if (ShouldIgnoreTicketDetailsResult(loadToken, ticketId))
        {
            return;
        }

        foreach (var row in customFieldRows)
        {
            CustomFields.Add(row);
        }

        OnPropertyChanged(nameof(HasCustomFields));
    }

    private bool ShouldIgnoreTicketDetailsResult(CancellationToken loadToken, int ticketId) =>
        loadToken.IsCancellationRequested || SelectedTicket?.Id != ticketId;

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
        _customFieldsCts?.Cancel();
        _customFieldsCts?.Dispose();
        _customFieldsCts = null;
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

    private void ClearPreview()
    {
        HasPreview = false;
        PreviewRows.Clear();
        NotifyPreviewChanged();
    }

    private void NotifyPreviewChanged()
    {
        OnPropertyChanged(nameof(PreviewSummary));
        OnPropertyChanged(nameof(CanBook));
    }
}
