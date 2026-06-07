namespace EasyRedmineTool.Core.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using EasyRedmineTool.Core;
using EasyRedmineTool.Core.Configuration;
using EasyRedmineTool.Core.Models.Tickets;
using EasyRedmineTool.Core.Services.Interfaces;

using System.Collections.ObjectModel;

public partial class TicketListViewModel : ViewModelBase, IDisposable
{
    private readonly ITicketService _ticketService;
    private readonly IAppSettingsService _appSettingsService;
    private readonly HashSet<int> _favoriteTicketIds = [];
    private CancellationTokenSource? _operationCts;

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
        var operationCts = BeginOperation();
        var cancellationToken = operationCts.Token;

        try
        {
            IsBusy = true;
            StatusMessage = "Tickets werden geladen ...";
            Tickets.Clear();

            var (baseUrl, apiKey) = LoadCredentials();
            var result = await _ticketService.GetTicketsForListAsync(baseUrl, apiKey, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            foreach (var ticket in result.Tickets)
            {
                Tickets.Add(CreateTicketItem(ticket));
            }

            PersistCurrentState();
            StatusMessage = result.TimeEntryTicketCount == 0
                ? $"{result.Tickets.Count} Ticket(s) geladen ({result.OpenTicketCount} offen zugewiesen)."
                : $"{result.Tickets.Count} Ticket(s) geladen ({result.OpenTicketCount} offen zugewiesen, {result.TimeEntryTicketCount} mit Zeiteinträgen im letzten Jahr).";
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

    private (string BaseUrl, string ApiKey) LoadCredentials()
    {
        var settings = _appSettingsService.Load();
        return (settings.BaseUrl, settings.ApiKey);
    }

    private void PersistCurrentState()
    {
        _appSettingsService.Update(settings =>
        {
            settings.CachedTickets = Tickets.Select(t => t.Ticket).ToList();
            settings.FavoriteTicketIds = _favoriteTicketIds.ToList();
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
