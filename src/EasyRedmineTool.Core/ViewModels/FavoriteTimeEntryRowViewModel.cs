namespace EasyRedmineTool.Core.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using EasyRedmineTool.Core;
using EasyRedmineTool.Core.Configuration;
using EasyRedmineTool.Core.Models.TimeEntries;
using EasyRedmineTool.Core.Models.Tickets;
using EasyRedmineTool.Core.Services.Interfaces;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;

public partial class FavoriteTimeEntryRowViewModel : ViewModelBase
{
    private readonly TimeEntriesViewModel _parent;
    private readonly IAppSettingsService _appSettingsService;
    private readonly ITimeEntryService _timeEntryService;

    [ObservableProperty]
    private string hours = AppConstants.DefaultHours;

    [ObservableProperty]
    private DateTime? spentOn = DateTime.Today;

    [ObservableProperty]
    private DateTime calendarDisplayDate = DateTime.Today;

    [ObservableProperty]
    private TimeEntryActivityDto? selectedActivity;

    [ObservableProperty]
    private string comments = string.Empty;

    [ObservableProperty]
    private bool areDetailsExpanded;

    [ObservableProperty]
    private bool isSubmitting;

    [ObservableProperty]
    private string rowStatusMessage = string.Empty;

    [ObservableProperty]
    private string validationMessage = string.Empty;

    public bool HasValidationMessage => !string.IsNullOrWhiteSpace(ValidationMessage);

    public bool HasRowStatusMessage => !string.IsNullOrWhiteSpace(RowStatusMessage);

    public bool HasEntryDetails =>
        !string.IsNullOrWhiteSpace(Comments) || CustomFields.Any(customField => !string.IsNullOrWhiteSpace(customField.Value));

    public string EntryDetailsHeader =>
        HasEntryDetails ? "Kommentar und Zusatzfelder · ausgefüllt" : "Kommentar und Zusatzfelder (optional)";

    [ObservableProperty]
    private bool isFocused;

    public FavoriteTimeEntryRowViewModel(
        TimeEntriesViewModel parent,
        IssueDto ticket,
        IAppSettingsService appSettingsService,
        ITimeEntryService timeEntryService)
    {
        _parent = parent;
        Ticket = ticket;
        _appSettingsService = appSettingsService;
        _timeEntryService = timeEntryService;
    }

    public IssueDto Ticket { get; }

    public ObservableCollection<TimeEntryActivityDto> Activities { get; } = [];

    public ObservableCollection<TimeEntryCustomFieldRowViewModel> CustomFields { get; } = [];

    public bool HasCustomFields => CustomFields.Count > 0;

    public string SelectedDateLabel =>
        SpentOn?.ToString("dddd, dd.MM.yyyy", CultureInfo.GetCultureInfo("de-DE")) ?? "Kein Datum";

    partial void OnSpentOnChanged(DateTime? value)
    {
        if (value.HasValue)
        {
            CalendarDisplayDate = value.Value;
        }

        OnPropertyChanged(nameof(SelectedDateLabel));
        ValidationMessage = string.Empty;
    }

    partial void OnHoursChanged(string value)
    {
        ValidationMessage = string.Empty;
        RowStatusMessage = string.Empty;
    }

    partial void OnCommentsChanged(string value) =>
        OnPropertyChanged(nameof(EntryDetailsHeader));

    partial void OnSelectedActivityChanged(TimeEntryActivityDto? value)
    {
        ValidationMessage = string.Empty;
        RowStatusMessage = string.Empty;
    }

    partial void OnValidationMessageChanged(string value) =>
        OnPropertyChanged(nameof(HasValidationMessage));

    partial void OnRowStatusMessageChanged(string value) =>
        OnPropertyChanged(nameof(HasRowStatusMessage));

    public async Task LoadActivitiesAsync(CancellationToken cancellationToken = default)
    {
        var settings = _appSettingsService.Load();
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            return;
        }

        var loadedActivities = await _timeEntryService.GetActivitiesAsync(
            settings.BaseUrl,
            settings.ApiKey,
            Ticket.Id,
            Ticket.Project?.Id,
            cancellationToken);

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        Activities.Clear();
        foreach (var activity in loadedActivities.OrderBy(a => a.Name))
        {
            Activities.Add(activity);
        }

        if (SelectedActivity is null || Activities.All(a => a.Id != SelectedActivity.Id))
        {
            SelectedActivity = Activities.FirstOrDefault();
        }

        CustomFields.Clear();
        var recentValues = await _timeEntryService.GetRecentCustomFieldValuesAsync(
            settings.BaseUrl,
            settings.ApiKey,
            Ticket.Id,
            Ticket.Project?.Id,
            cancellationToken);

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        foreach (var row in TimeEntryCustomFieldSupport.CreateRows(recentValues, settings))
        {
            row.PropertyChanged += OnCustomFieldChanged;
            CustomFields.Add(row);
        }

        OnPropertyChanged(nameof(HasCustomFields));
        OnPropertyChanged(nameof(HasEntryDetails));
        OnPropertyChanged(nameof(EntryDetailsHeader));
        if (CustomFields.Any(field => field.IsRequired))
        {
            AreDetailsExpanded = true;
        }
    }

    private void OnCustomFieldChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TimeEntryCustomFieldRowViewModel.Value) or nameof(TimeEntryCustomFieldRowViewModel.SelectedValue))
        {
            OnPropertyChanged(nameof(HasEntryDetails));
            OnPropertyChanged(nameof(EntryDetailsHeader));
        }
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

    [RelayCommand]
    private void OpenInBrowser()
    {
        var settings = _appSettingsService.Load();
        RedmineLinks.OpenIssueInBrowser(settings.BaseUrl, Ticket.Id);
    }

    [RelayCommand]
    private async Task CreateTimeEntryAsync()
    {
        if (IsSubmitting)
        {
            return;
        }

        if (!TimeEntryHours.TryParsePositive(Hours, out var parsedHours))
        {
            ValidationMessage = "Bitte gültige Stunden eingeben.";
            return;
        }

        if (SelectedActivity is null)
        {
            ValidationMessage = "Bitte eine Aktivität auswählen.";
            return;
        }

        if (SpentOn is null)
        {
            ValidationMessage = "Bitte ein Datum auswählen.";
            return;
        }

        var customFieldError = TimeEntryCustomFieldSupport.Validate(CustomFields);
        if (customFieldError is not null)
        {
            ValidationMessage = customFieldError;
            return;
        }

        var settings = _appSettingsService.Load();
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            _parent.SetStatusMessage("API-Key fehlt. Bitte zuerst Einstellungen speichern.");
            return;
        }

        var submittedDate = SpentOn.Value;
        try
        {
            IsSubmitting = true;
            ValidationMessage = string.Empty;
            RowStatusMessage = "Zeiteintrag wird erstellt …";

            var result = await _timeEntryService.CreateTimeEntryAsync(
                settings.BaseUrl,
                settings.ApiKey,
                new TimeEntryCreateRequest
                {
                    IssueId = Ticket.Id,
                    Hours = parsedHours,
                    SpentOn = RedmineDates.FormatSpentOn(submittedDate),
                    ActivityId = SelectedActivity.Id,
                    Comments = Comments,
                    CustomFields = TimeEntryCustomFieldSupport.BuildValues(CustomFields).ToList()
                });

            RowStatusMessage = result.Success
                ? $"{parsedHours.ToString("0.##", CultureInfo.GetCultureInfo("de-DE"))} h für {submittedDate:dd.MM.yyyy} gebucht."
                : result.Message;
            _parent.SetStatusMessage(result.Message);
            if (result.Success)
            {
                TimeEntryCustomFieldSupport.SaveDefaults(_appSettingsService, CustomFields);
                Comments = string.Empty;
                await _parent.HandleEntryCreatedAsync(this, submittedDate);
            }
        }
        catch (Exception ex)
        {
            RowStatusMessage = ex.Message;
            _parent.SetStatusMessage($"Ticket #{Ticket.Id}: {ex.Message}");
        }
        finally
        {
            IsSubmitting = false;
        }
    }
}
