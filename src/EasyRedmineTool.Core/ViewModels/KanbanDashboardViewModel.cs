namespace EasyRedmineTool.Core.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyRedmineTool.Core.Configuration;
using EasyRedmineTool.Core.Models.Tickets;
using EasyRedmineTool.Core.Services.Interfaces;
using System.Collections.ObjectModel;

public partial class KanbanDashboardViewModel : ViewModelBase, IDisposable
{
    private readonly ITicketService _ticketService;
    private readonly IAppSettingsService _settingsService;
    private CancellationTokenSource? _loadCts;
    private bool _isApplyingSettings;
    private IReadOnlyList<IssueDto> _allTickets = [];
    private IReadOnlyList<StatusDto> _statuses = [];

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string statusMessage = "";
    [ObservableProperty] private string ticketSearch = "";

    public ObservableCollection<ProjectChoice> Projects { get; } = [];
    public ObservableCollection<DashboardTicketChoice> AvailableTickets { get; } = [];
    public ObservableCollection<KanbanColumn> Columns { get; } = [];
    public IEnumerable<DashboardTicketChoice> FilteredAvailableTickets =>
        string.IsNullOrWhiteSpace(TicketSearch)
            ? AvailableTickets
            : AvailableTickets.Where(item => item.SearchText.Contains(TicketSearch.Trim(), StringComparison.CurrentCultureIgnoreCase));

    public KanbanDashboardViewModel(ITicketService ticketService, IAppSettingsService settingsService)
    {
        _ticketService = ticketService;
        _settingsService = settingsService;
    }

    partial void OnTicketSearchChanged(string value) => OnPropertyChanged(nameof(FilteredAvailableTickets));

    public async Task LoadAsync()
    {
        var settings = _settingsService.Load();
        if (string.IsNullOrWhiteSpace(settings.ApiKey) || string.IsNullOrWhiteSpace(settings.BaseUrl))
        {
            StatusMessage = "Bitte zuerst die Redmine-Verbindung in den Einstellungen konfigurieren.";
            return;
        }

        CancelLoad();
        var cts = new CancellationTokenSource();
        _loadCts = cts;
        IsBusy = true;
        StatusMessage = "Tickets werden geladen …";
        try
        {
            var result = await _ticketService.GetTicketsForListAsync(settings.BaseUrl, settings.ApiKey,
                new TicketLoadFilter { Assignee = TicketAssigneeFilter.All, StatusKind = TicketStatusFilterKind.All }, cts.Token);
            var statuses = await _ticketService.GetIssueStatusesAsync(settings.BaseUrl, settings.ApiKey, cts.Token);
            if (cts.IsCancellationRequested) return;

            _isApplyingSettings = true;
            _allTickets = result.Tickets;
            _statuses = statuses;
            Projects.Clear();
            foreach (var project in result.Tickets.Where(ticket => ticket.Project is not null)
                         .Select(ticket => ticket.Project!).DistinctBy(project => project.Id).OrderBy(project => project.Name))
            {
                var choice = new ProjectChoice(project.Id, project.Name, settings.DashboardProjectIds.Contains(project.Id));
                choice.PropertyChanged += OnSelectionChanged;
                Projects.Add(choice);
            }

            AvailableTickets.Clear();
            foreach (var ticket in result.Tickets.OrderBy(ticket => ticket.Project?.Name).ThenBy(ticket => ticket.Id))
            {
                var choice = new DashboardTicketChoice(ticket, settings.DashboardTicketIds.Contains(ticket.Id));
                choice.PropertyChanged += OnSelectionChanged;
                AvailableTickets.Add(choice);
            }
            _isApplyingSettings = false;
            RebuildBoard(result.Tickets, statuses);
            StatusMessage = result.Tickets.Count == 0
                ? "Für diesen Redmine-Zugang wurden keine Tickets gefunden."
                : $"{SelectedIssues(result.Tickets).Count} Tickets im Teamboard · {result.Tickets.Count} verfügbar";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            StatusMessage = $"Tickets konnten nicht geladen werden: {ex.Message}";
        }
        finally
        {
            if (_loadCts == cts) _loadCts = null;
            cts.Dispose();
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task Reload() => LoadAsync();

    private void OnSelectionChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_isApplyingSettings || e.PropertyName != nameof(ProjectChoice.IsSelected) && e.PropertyName != nameof(DashboardTicketChoice.IsSelected)) return;
        var settings = _settingsService.Load();
        settings.DashboardProjectIds = Projects.Where(item => item.IsSelected).Select(item => item.Id).ToList();
        settings.DashboardTicketIds = AvailableTickets.Where(item => item.IsSelected).Select(item => item.Ticket.Id).ToList();
        _settingsService.Save(settings);
        RebuildBoard(_allTickets, _statuses);
        StatusMessage = $"{SelectedIssues(_allTickets).Count} Tickets im Teamboard · {AvailableTickets.Count} verfügbar";
    }

    private void RebuildBoard(IReadOnlyList<IssueDto> allTickets, IReadOnlyList<StatusDto> statuses)
    {
        var chosen = SelectedIssues(allTickets);
        Columns.Clear();
        var orderedStatuses = statuses.OrderBy(status => status.Is_Closed).ThenBy(status => status.Name).ToList();
        foreach (var status in orderedStatuses)
        {
            Columns.Add(new KanbanColumn(status.Name, chosen.Where(ticket => ticket.Status?.Id == status.Id).ToList()));
        }
        foreach (var group in chosen.Where(ticket => ticket.Status is null || orderedStatuses.All(status => status.Id != ticket.Status.Id))
                     .GroupBy(ticket => ticket.Status?.Name ?? "Unbekannt").OrderBy(group => group.Key))
        {
            Columns.Add(new KanbanColumn(group.Key, group.ToList()));
        }
    }

    private List<IssueDto> SelectedIssues(IEnumerable<IssueDto> tickets)
    {
        var projectIds = Projects.Where(project => project.IsSelected).Select(project => project.Id).ToHashSet();
        var ticketIds = AvailableTickets.Where(item => item.IsSelected).Select(item => item.Ticket.Id).ToHashSet();
        return tickets.Where(ticket => ticketIds.Contains(ticket.Id) || ticket.Project is not null && projectIds.Contains(ticket.Project.Id))
            .OrderBy(ticket => ticket.Project?.Name).ThenBy(ticket => ticket.Id).ToList();
    }

    private void CancelLoad()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = null;
    }

    public void Dispose()
    {
        CancelLoad();
        GC.SuppressFinalize(this);
    }
}

public partial class ProjectChoice : ObservableObject
{
    public int Id { get; }
    public string Name { get; }
    [ObservableProperty] private bool isSelected;
    public ProjectChoice(int id, string name, bool selected) { Id = id; Name = name; IsSelected = selected; }
}

public partial class DashboardTicketChoice : ObservableObject
{
    public IssueDto Ticket { get; }
    public string SearchText => $"#{Ticket.Id} {Ticket.Subject} {Ticket.Project?.Name}";
    [ObservableProperty] private bool isSelected;
    public DashboardTicketChoice(IssueDto ticket, bool selected) { Ticket = ticket; IsSelected = selected; }
}

public sealed class KanbanColumn(string title, IReadOnlyList<IssueDto> tickets)
{
    public string Title { get; } = title;
    public IReadOnlyList<IssueDto> Tickets { get; } = tickets;
    public int Count => Tickets.Count;
    public bool HasTickets => Count > 0;
}
