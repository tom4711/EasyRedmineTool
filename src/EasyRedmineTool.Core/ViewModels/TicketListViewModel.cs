using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using EasyRedmineTool.Core.Models.Tickets;
using EasyRedmineTool.Core.Services.Interfaces;

using System.Buffers.Text;
using System.Collections.ObjectModel;

namespace EasyRedmineTool.Core.ViewModels;

public partial class TicketListViewModel : ViewModelBase
{
    private readonly ITicketService _ticketService;

    [ObservableProperty]
    private string baseUrl = "https://projects.hawe.com/";

    [ObservableProperty]
    private string apiKey = "REDACTED";

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    public ObservableCollection<IssueDto> Tickets { get; } = new();

    public TicketListViewModel(ITicketService ticketService)
    {
        _ticketService = ticketService;
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

            StatusMessage = $"{Tickets.Count} Ticket(s) geladen.";
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
}

