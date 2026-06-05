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
    private TicketListItemViewModel? selectedTicket;

    public ObservableCollection<TicketListItemViewModel> Tickets { get; } = [];

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
            Tickets.Add(CreateTicketItem(ticket));
        }
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

            var result = await _ticketService.GetTicketsForListAsync(BaseUrl, ApiKey);

            foreach (var ticket in result.Tickets)
            {
                Tickets.Add(CreateTicketItem(ticket));
            }

            PersistCurrentState();
            StatusMessage = result.TimeEntryTicketCount == 0
                ? $"{result.Tickets.Count} Ticket(s) geladen ({result.OpenTicketCount} offen zugewiesen)."
                : $"{result.Tickets.Count} Ticket(s) geladen ({result.OpenTicketCount} offen zugewiesen, {result.TimeEntryTicketCount} mit Zeiteinträgen im letzten Jahr).";
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

        if (Tickets.Any(t => t.Ticket.Id == ticketId))
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

            Tickets.Add(CreateTicketItem(ticket));
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
    private void ToggleFavoriteForTicket(TicketListItemViewModel? ticketItem)
    {
        if (ticketItem is null)
        {
            return;
        }

        ToggleFavorite(ticketItem);
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

    private void PersistCurrentState()
    {
        var current = _appSettingsService.Load();
        _appSettingsService.Save(new AppSettings
        {
            BaseUrl = BaseUrl,
            ApiKey = ApiKey,
            CachedTickets = Tickets.Select(t => t.Ticket).ToList(),
            FavoriteTicketIds = _favoriteTicketIds.ToList()
        });
    }
}
