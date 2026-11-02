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
    private TimeEntryDto _entry;
    private bool _isDeleted;
    private readonly string _connectionIdentity;
    private readonly int _connectionVersion;
    private bool IsCurrentConnection => _connectionVersion == _parent.ConnectionVersion && _connectionIdentity == _parent.CurrentConnectionIdentity();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWarningMessage))]
    private string warningMessage = string.Empty;
    public bool HasWarningMessage => !string.IsNullOrWhiteSpace(WarningMessage);

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
        _connectionVersion = parent.ConnectionVersion;
        var settings = appSettingsService.Load();
        _connectionIdentity = ConnectionIdentity.Get(settings.BaseUrl, settings.ApiKey);

        Hours = entry.Hours.ToString("0.##", CultureInfo.InvariantCulture);
        Comments = entry.Comments;
    }

    internal double BookedHours => _entry.Hours;
    internal void UpdateEntry(TimeEntryDto entry, IssueDto? ticket)
    {
        if (IsBusy || IsEditing) return;
        _entry = entry;
        CachedTicket = ticket;
        NotifyCommittedValues();
    }

    private void NotifyCommittedValues()
    {
        OnPropertyChanged(nameof(TicketLabel));
        OnPropertyChanged(nameof(HoursDisplay));
        OnPropertyChanged(nameof(ActivityName));
        OnPropertyChanged(nameof(CommentsDisplay));
    }

    public int EntryId => _entry.Id;

    public int IssueId => _entry.GetIssueId();

    public IssueDto? CachedTicket { get; private set; }

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
        if (!IsCurrentConnection || _isDeleted) return;
        if (IsBusy)
        {
            return;
        }

        if (!TimeEntryHours.TryParsePositive(Hours, out var parsedHours))
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

        var submittedComments = Comments;
        var submittedActivity = SelectedActivity;
        var submittedFields = CustomFields.Select(field => new TimeEntryCustomFieldRowViewModel(
            field.Id, field.Name, field.IsRequired, field.HasPossibleValues, field.PossibleValues.ToList(), field.Value)).ToList();
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
                    ActivityId = submittedActivity.Id,
                    Comments = submittedComments,
                    CustomFields = TimeEntryCustomFieldSupport.BuildValues(submittedFields).ToList()
                });

            if (!IsCurrentConnection || _isDeleted) return;
            _parent.SetStatusMessage(result.Message);
            if (result.Success)
            {
                _entry.Hours = parsedHours;
                _entry.Comments = submittedComments;
                _entry.Activity = submittedActivity;
                _entry.Activity_Id = submittedActivity.Id;
                _entry.Custom_Fields = submittedFields.Select(field => new TimeEntryCustomFieldValueDto
                {
                    Id = field.Id, Name = field.Name, Value = field.Value.Trim()
                }).ToList();
                NotifyCommittedValues();
                IsEditing = false;
                try
                {
                    TimeEntryCustomFieldSupport.SaveDefaults(_appSettingsService, submittedFields);
                    await _appSettingsService.FlushAsync();
                }
                catch (Exception)
                {
                    WarningMessage = "Die Änderung ist gespeichert; lokale Vorgaben konnten nicht gespeichert werden.";
                    if (IsCurrentConnection) _parent.SetWarningMessage(WarningMessage);
                }
                if (IsCurrentConnection) await RefreshAfterCommitAsync();
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
        if (!IsCurrentConnection || _isDeleted) return;
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

            if (!IsCurrentConnection || _isDeleted) return;
            _parent.SetStatusMessage(result.Message);
            if (result.Success)
            {
                IsDeleteConfirmationVisible = false;
                _isDeleted = true;
                _parent.RemoveCommittedEntry(this);
                await RefreshAfterCommitAsync();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshAfterCommitAsync()
    {
        var commitMessage = _parent.StatusMessage;
        try
        {
            if (await _parent.RefreshTodayCoreAsync()) return;
        }
        catch (Exception) { }
        if (IsCurrentConnection)
        {
            _parent.SetStatusMessage(commitMessage);
            WarningMessage = "Die Änderung ist gespeichert; die Tagesübersicht konnte nicht aktualisiert werden.";
            _parent.SetWarningMessage(WarningMessage);
        }
    }

    private async Task LoadEditFormDataAsync()
    {
        try { await LoadEditFormCoreAsync(); }
        catch (Exception) { if (IsCurrentConnection) WarningMessage = "Die Formulardaten konnten nicht geladen werden."; }
    }

    private async Task LoadEditFormCoreAsync()
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

        if (!IsCurrentConnection || !IsEditing) return;
        Activities.Clear();
        foreach (var activity in loadedActivities.OrderBy(activity => activity.Name))
        {
            Activities.Add(activity);
        }

        SelectedActivity = Activities.FirstOrDefault(activity => activity.Id == _entry.Activity_Id)
            ?? Activities.FirstOrDefault(activity => activity.Id == _entry.Activity?.Id)
            ?? Activities.FirstOrDefault();

        var recentValues = await _timeEntryService.GetRecentCustomFieldValuesAsync(
            settings.BaseUrl,
            settings.ApiKey,
            IssueId,
            projectId);

        if (!IsCurrentConnection || !IsEditing) return;
        CustomFields.Clear();
        foreach (var row in TimeEntryCustomFieldSupport.CreateRows(
                     recentValues,
                     settings,
                     _entry.Custom_Fields))
        {
            CustomFields.Add(row);
        }

        OnPropertyChanged(nameof(HasCustomFields));
    }
}
