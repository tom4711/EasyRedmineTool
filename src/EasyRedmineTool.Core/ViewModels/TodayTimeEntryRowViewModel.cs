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

public partial class TodayTimeEntryRowViewModel : ViewModelBase
{
    private readonly TimeEntriesViewModel _parent;
    private readonly IAppSettingsService _appSettingsService;
    private readonly ITimeEntryService _timeEntryService;
    private readonly TimeEntryDto _entry;

    [ObservableProperty]
    private string hours = string.Empty;

    [ObservableProperty]
    private string comments = string.Empty;

    [ObservableProperty]
    private TimeEntryActivityDto? selectedActivity;

    [ObservableProperty]
    private bool isEditing;

    [ObservableProperty]
    private bool isDeleteConfirmationVisible;

    [ObservableProperty]
    private bool isBusy;

    public TodayTimeEntryRowViewModel(
        TimeEntriesViewModel parent,
        TimeEntryDto entry,
        IssueDto? cachedTicket,
        IAppSettingsService appSettingsService,
        ITimeEntryService timeEntryService)
    {
        _parent = parent;
        _entry = entry;
        CachedTicket = cachedTicket;
        _appSettingsService = appSettingsService;
        _timeEntryService = timeEntryService;

        Hours = entry.Hours.ToString("0.##", CultureInfo.InvariantCulture);
        Comments = entry.Comments;
    }

    public int EntryId => _entry.Id;

    public int IssueId => _entry.GetIssueId();

    public IssueDto? CachedTicket { get; }

    public ObservableCollection<TimeEntryActivityDto> Activities { get; } = [];

    public ObservableCollection<TimeEntryCustomFieldRowViewModel> CustomFields { get; } = [];

    public bool HasCustomFields => CustomFields.Count > 0;

    public string TicketLabel
    {
        get
        {
            var subject = CachedTicket?.Subject;
            if (string.IsNullOrWhiteSpace(subject))
            {
                subject = _entry.Issue?.GetDisplaySubject();
            }

            return string.IsNullOrWhiteSpace(subject)
                ? $"#{IssueId}"
                : $"#{IssueId} · {subject}";
        }
    }

    public string HoursDisplay =>
        $"{_entry.Hours.ToString("0.##", CultureInfo.GetCultureInfo("de-DE"))} h";

    public string ActivityName =>
        _entry.Activity?.Name ?? $"Aktivität #{_entry.Activity_Id}";

    public string CommentsDisplay =>
        string.IsNullOrWhiteSpace(_entry.Comments) ? "—" : _entry.Comments;

    partial void OnIsEditingChanged(bool value)
    {
        if (value)
        {
            Hours = _entry.Hours.ToString("0.##", CultureInfo.InvariantCulture);
            Comments = _entry.Comments;
            _ = LoadEditFormDataAsync();
        }
        else
        {
            IsDeleteConfirmationVisible = false;
            CustomFields.Clear();
            OnPropertyChanged(nameof(HasCustomFields));
        }
    }

    [RelayCommand]
    private void BeginEdit()
    {
        _parent.EnsureTodayOverviewExpanded();
        IsDeleteConfirmationVisible = false;
        IsEditing = true;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
    }

    [RelayCommand]
    private void RequestDelete()
    {
        _parent.EnsureTodayOverviewExpanded();
        IsEditing = false;
        IsDeleteConfirmationVisible = true;
    }

    [RelayCommand]
    private void CancelDelete()
    {
        IsDeleteConfirmationVisible = false;
    }

    [RelayCommand]
    private async Task SaveEditAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (!double.TryParse(Hours, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedHours) || parsedHours <= 0)
        {
            _parent.SetStatusMessage($"Eintrag #{EntryId}: Bitte gültige Stunden eingeben.");
            return;
        }

        if (SelectedActivity is null)
        {
            _parent.SetStatusMessage($"Eintrag #{EntryId}: Bitte eine Aktivität auswählen.");
            return;
        }

        var customFieldError = TimeEntryCustomFieldSupport.Validate(CustomFields);
        if (customFieldError is not null)
        {
            _parent.SetStatusMessage($"Eintrag #{EntryId}: {customFieldError}");
            return;
        }

        var settings = _appSettingsService.Load();
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            _parent.SetStatusMessage("API-Key fehlt. Bitte zuerst Einstellungen speichern.");
            return;
        }

        try
        {
            IsBusy = true;
            _parent.SetStatusMessage($"Eintrag #{EntryId} wird aktualisiert …");

            var result = await _timeEntryService.UpdateTimeEntryAsync(
                settings.BaseUrl,
                settings.ApiKey,
                EntryId,
                new TimeEntryUpdateRequest
                {
                    IssueId = IssueId,
                    Hours = parsedHours,
                    SpentOn = _entry.Spent_On,
                    ActivityId = SelectedActivity.Id,
                    Comments = Comments,
                    CustomFields = TimeEntryCustomFieldSupport.BuildValues(CustomFields).ToList()
                });

            _parent.SetStatusMessage(result.Message);
            if (result.Success)
            {
                TimeEntryCustomFieldSupport.SaveDefaults(_appSettingsService, CustomFields);
                IsEditing = false;
                await _parent.ReloadTodayBookedHoursAsync();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ConfirmDeleteAsync()
    {
        if (IsBusy)
        {
            return;
        }

        var settings = _appSettingsService.Load();
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            _parent.SetStatusMessage("API-Key fehlt. Bitte zuerst Einstellungen speichern.");
            return;
        }

        try
        {
            IsBusy = true;
            _parent.SetStatusMessage($"Eintrag #{EntryId} wird gelöscht …");

            var result = await _timeEntryService.DeleteTimeEntryAsync(
                settings.BaseUrl,
                settings.ApiKey,
                EntryId);

            _parent.SetStatusMessage(result.Message);
            if (result.Success)
            {
                IsDeleteConfirmationVisible = false;
                await _parent.ReloadTodayBookedHoursAsync();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadEditFormDataAsync()
    {
        var settings = _appSettingsService.Load();
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            return;
        }

        var projectId = CachedTicket?.Project?.Id;

        var loadedActivities = await _timeEntryService.GetActivitiesAsync(
            settings.BaseUrl,
            settings.ApiKey,
            IssueId,
            projectId);

        Activities.Clear();
        foreach (var activity in loadedActivities.OrderBy(activity => activity.Name))
        {
            Activities.Add(activity);
        }

        SelectedActivity = Activities.FirstOrDefault(activity => activity.Id == _entry.Activity_Id)
            ?? Activities.FirstOrDefault(activity => activity.Id == _entry.Activity?.Id)
            ?? Activities.FirstOrDefault();

        var definitions = await _timeEntryService.GetCustomFieldDefinitionsAsync(
            settings.BaseUrl,
            settings.ApiKey,
            projectId);

        var recentValues = await _timeEntryService.GetRecentCustomFieldValuesAsync(
            settings.BaseUrl,
            settings.ApiKey,
            IssueId,
            projectId);

        CustomFields.Clear();
        foreach (var row in TimeEntryCustomFieldSupport.CreateRows(
                     definitions,
                     recentValues,
                     settings,
                     _entry.Custom_Fields))
        {
            CustomFields.Add(row);
        }

        OnPropertyChanged(nameof(HasCustomFields));
    }
}
