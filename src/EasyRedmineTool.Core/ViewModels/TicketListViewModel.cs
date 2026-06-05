namespace EasyRedmineTool.Core.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using EasyRedmineTool.Core.Configuration;
using EasyRedmineTool.Core.Models.Tickets;
using EasyRedmineTool.Core.Services.Interfaces;

using System.Collections.ObjectModel;
using System.Linq;

public partial class TicketListViewModel : ViewModelBase
{
    private readonly ITicketService _ticketService;
    private readonly IAppSettingsService _appSettingsService;
    private readonly HashSet<int> _favoriteTicketIds = [];

    [ObservableProperty]
    private string baseUrl = "https://projects.hawe.com/";

    [ObservableProperty]
    private string apiKey = string.Empty;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string ticketIdToAdd = string.Empty;

    [ObservableProperty]
    private IssueDto? selectedTicket;

    [ObservableProperty]
    private bool isSelectedFavorite;

    public ObservableCollection<IssueDto> Tickets { get; } = [];

    public TicketListViewModel(ITicketService ticketService, IAppSettingsService appSettingsService)
    {
        _ticketService = ticketService;
        _appSettingsService = appSettingsService;

        ReloadSettings();
    }

    public void ReloadSettings()
    {
        var settings = _appSettingsService.Load();
        BaseUrl = settings.BaseUrl;
        ApiKey = settings.ApiKey;

        _favoriteTicketIds.Clear();
        foreach (var id in settings.FavoriteTicketIds)
        {
            _favoriteTicketIds.Add(id);
        }

        Tickets.Clear();
        foreach (var ticket in settings.CachedTickets)
        {
            Tickets.Add(ticket);
        }

        UpdateSelectedFavoriteState();
    }

    [RelayCommand]
    private async Task LoadTicketsAsync()
    {
        if (IsBusy)
            return;

        try
        {
            IsBusy = true;
            StatusMessage = "Tickets werden geladen ...";
            Tickets.Clear();

            var tickets = await _ticketService.GetMyOpenIssuesAsync(BaseUrl, ApiKey);

            foreach (var ticket in tickets)
            {
                Tickets.Add(ticket);
            }

            PersistCurrentState();
            StatusMessage = $"{Tickets.Count} Ticket(s) geladen und gecacht.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fehler beim Laden: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
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

        if (Tickets.Any(t => t.Id == ticketId))
        {
            StatusMessage = "Ticket ist bereits in der Liste vorhanden.";
            return;
        }

        try
        {
            IsBusy = true;
            var ticket = await _ticketService.GetIssueByIdAsync(BaseUrl, ApiKey, ticketId);

            if (ticket is null)
            {
                StatusMessage = "Ticket wurde nicht gefunden.";
                return;
            }

            Tickets.Add(ticket);
            TicketIdToAdd = string.Empty;
            PersistCurrentState();
            StatusMessage = $"Ticket #{ticket.Id} wurde hinzugefügt.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fehler beim Hinzufügen: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ToggleSelectedFavorite()
    {
        if (SelectedTicket is null)
        {
            StatusMessage = "Bitte zuerst ein Ticket auswählen.";
            return;
        }

        if (_favoriteTicketIds.Contains(SelectedTicket.Id))
        {
            _favoriteTicketIds.Remove(SelectedTicket.Id);
            StatusMessage = $"Ticket #{SelectedTicket.Id} aus Favoriten entfernt.";
        }
        else
        {
            _favoriteTicketIds.Add(SelectedTicket.Id);
            StatusMessage = $"Ticket #{SelectedTicket.Id} als Favorit markiert.";
        }

        PersistCurrentState();
        UpdateSelectedFavoriteState();
    }

    private void UpdateSelectedFavoriteState()
    {
        IsSelectedFavorite = SelectedTicket is not null && _favoriteTicketIds.Contains(SelectedTicket.Id);
    }

    partial void OnSelectedTicketChanged(IssueDto? value)
    {
        UpdateSelectedFavoriteState();
    }

    private void PersistCurrentState()
    {
        var current = _appSettingsService.Load();
        _appSettingsService.Save(new AppSettings
        {
            BaseUrl = BaseUrl,
            ApiKey = ApiKey,
            CachedTickets = Tickets.ToList(),
            FavoriteTicketIds = _favoriteTicketIds.ToList()
        });
    }
}

