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
    private bool isSubmitting;

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
    }

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
        var definitions = await _timeEntryService.GetCustomFieldDefinitionsAsync(
            settings.BaseUrl,
            settings.ApiKey,
            Ticket.Project?.Id,
            cancellationToken);

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

        foreach (var row in TimeEntryCustomFieldSupport.CreateRows(definitions, recentValues, settings))
        {
            CustomFields.Add(row);
        }

        OnPropertyChanged(nameof(HasCustomFields));
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

        if (!double.TryParse(Hours, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedHours) || parsedHours <= 0)
        {
            _parent.SetStatusMessage($"Ticket #{Ticket.Id}: Bitte gültige Stunden eingeben.");
            return;
        }

        if (SelectedActivity is null)
        {
            _parent.SetStatusMessage($"Ticket #{Ticket.Id}: Bitte eine Aktivität auswählen.");
            return;
        }

        if (SpentOn is null)
        {
            _parent.SetStatusMessage($"Ticket #{Ticket.Id}: Bitte ein Datum auswählen.");
            return;
        }

        var customFieldError = TimeEntryCustomFieldSupport.Validate(CustomFields);
        if (customFieldError is not null)
        {
            _parent.SetStatusMessage($"Ticket #{Ticket.Id}: {customFieldError}");
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
            IsSubmitting = true;
            _parent.SetStatusMessage($"Zeiteintrag für #{Ticket.Id} wird erstellt …");

            var result = await _timeEntryService.CreateTimeEntryAsync(
                settings.BaseUrl,
                settings.ApiKey,
                new TimeEntryCreateRequest
                {
                    IssueId = Ticket.Id,
                    Hours = parsedHours,
                    SpentOn = RedmineDates.FormatSpentOn(SpentOn.Value),
                    ActivityId = SelectedActivity.Id,
                    Comments = Comments,
                    CustomFields = TimeEntryCustomFieldSupport.BuildValues(CustomFields).ToList()
                });

            _parent.SetStatusMessage(result.Message);
            if (result.Success)
            {
                TimeEntryCustomFieldSupport.SaveDefaults(_appSettingsService, CustomFields);
                Comments = string.Empty;
                await _parent.HandleEntryCreatedAsync(this, SpentOn.Value);
            }
        }
        finally
        {
            IsSubmitting = false;
        }
    }
}
