namespace EasyRedmineTool.Core.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using EasyRedmineTool.Core.Models.Tickets;
using EasyRedmineTool.Core.Services.Interfaces;

using System.Collections.ObjectModel;

public partial class TicketListViewModel : ViewModelBase
{
    private readonly ITicketService _ticketService;
    private readonly IAppSettingsService _appSettingsService;

    [ObservableProperty]
    private string baseUrl = "https://projects.hawe.com/";

    [ObservableProperty]
    private string apiKey = string.Empty;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool isBusy;

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

