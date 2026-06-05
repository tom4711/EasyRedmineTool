namespace EasyRedmineTool.Core.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using EasyRedmineTool.Core.Models.TimeEntries;
using EasyRedmineTool.Core.Models.Tickets;
using EasyRedmineTool.Core.Services.Interfaces;

using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

public partial class TimeEntriesViewModel : ViewModelBase
{
    private readonly IAppSettingsService _appSettingsService;
    private readonly ITimeEntryService _timeEntryService;

    [ObservableProperty]
    private IssueDto? selectedFavoriteTicket;

    [ObservableProperty]
    private string hours = "1";

    [ObservableProperty]
    private string spentOn = DateTime.Today.ToString("yyyy-MM-dd");

    [ObservableProperty]
    private TimeEntryActivityDto? selectedActivity;

    [ObservableProperty]
    private string comments = string.Empty;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    public ObservableCollection<IssueDto> FavoriteTickets { get; } = [];
    public ObservableCollection<TimeEntryActivityDto> Activities { get; } = [];

    public TimeEntriesViewModel(IAppSettingsService appSettingsService, ITimeEntryService timeEntryService)
    {
        _appSettingsService = appSettingsService;
        _timeEntryService = timeEntryService;

        ReloadFavorites();
        _ = ReloadActivitiesAsync();
    }

    [RelayCommand]
    public void ReloadFavorites()
    {
        var settings = _appSettingsService.Load();

        var favorites = settings.CachedTickets
            .Where(t => settings.FavoriteTicketIds.Contains(t.Id))
            .OrderBy(t => t.Id)
            .ToList();

        FavoriteTickets.Clear();
        foreach (var ticket in favorites)
        {
            FavoriteTickets.Add(ticket);
        }

        if (SelectedFavoriteTicket is null || !FavoriteTickets.Any(t => t.Id == SelectedFavoriteTicket.Id))
        {
            SelectedFavoriteTicket = FavoriteTickets.FirstOrDefault();
        }

        StatusMessage = FavoriteTickets.Count == 0
            ? "Keine Favoriten vorhanden. Bitte in der Ticketliste Favoriten markieren."
            : $"{FavoriteTickets.Count} Favorit(en) geladen.";
    }

    [RelayCommand]
    public async Task ReloadActivitiesAsync()
    {
        var settings = _appSettingsService.Load();
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            return;
        }

        var issueId = SelectedFavoriteTicket?.Id;
        var projectId = SelectedFavoriteTicket?.Project?.Id;

        var loadedActivities = await _timeEntryService.GetActivitiesAsync(
            settings.BaseUrl,
            settings.ApiKey,
            issueId,
            projectId);

        Activities.Clear();
        foreach (var activity in loadedActivities.OrderBy(a => a.Name))
        {
            Activities.Add(activity);
        }

        if (SelectedActivity is null || Activities.All(a => a.Id != SelectedActivity.Id))
        {
            SelectedActivity = Activities.FirstOrDefault();
        }

        if (Activities.Count == 0)
        {
            StatusMessage = "Keine Aktivitäten gefunden. Bitte API-Berechtigung/Projekt prüfen.";
        }
    }

    partial void OnSelectedFavoriteTicketChanged(IssueDto? value)
    {
        _ = ReloadActivitiesAsync();
    }

    [RelayCommand]
    private async Task CreateTimeEntryAsync()
    {
        if (IsBusy)
            return;

        if (SelectedFavoriteTicket is null)
        {
            StatusMessage = "Bitte ein Favoriten-Ticket auswählen.";
            return;
        }

        if (!double.TryParse(Hours, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedHours) || parsedHours <= 0)
        {
            StatusMessage = "Bitte gültige Stunden eingeben (z. B. 1.5).";
            return;
        }

        if (SelectedActivity is null)
        {
            StatusMessage = "Bitte eine Aktivität auswählen.";
            return;
        }

        if (!DateTime.TryParseExact(SpentOn, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            StatusMessage = "Datum muss im Format yyyy-MM-dd angegeben werden.";
            return;
        }

        var settings = _appSettingsService.Load();
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            StatusMessage = "API-Key fehlt. Bitte zuerst Einstellungen speichern.";
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Zeiteintrag wird erstellt ...";

            var result = await _timeEntryService.CreateTimeEntryAsync(
                settings.BaseUrl,
                settings.ApiKey,
                new TimeEntryCreateRequest
                {
                    IssueId = SelectedFavoriteTicket.Id,
                    Hours = parsedHours,
                    SpentOn = SpentOn,
                    ActivityId = SelectedActivity.Id,
                    Comments = Comments
                });

            StatusMessage = result.Message;
            if (result.Success)
            {
                Comments = string.Empty;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
