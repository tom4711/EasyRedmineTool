namespace EasyRedmineTool.Core.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyRedmineTool.Core.Configuration;
using EasyRedmineTool.Core.Models.Tickets;
using EasyRedmineTool.Core.Services.Interfaces;
using System.Collections.ObjectModel;
using System.Globalization;

public partial class KanbanDashboardViewModel : ViewModelBase, IDisposable
{
    private readonly ITicketService _ticketService;
    private readonly IAppSettingsService _settingsService;
    private CancellationTokenSource? _loadCts;
    private bool _isApplyingSettings;
    private List<IssueDto> _allTickets = [];
    private List<ProjectChoice> _allProjectChoices = [];
    private IReadOnlyList<StatusDto> _statuses = [];

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string statusMessage = "";
    [ObservableProperty] private string ticketIdToAdd = "";
    [ObservableProperty] private string? selectedProjectFilterField;
    [ObservableProperty] private string? selectedProjectFilterValue;

    public ObservableCollection<ProjectChoice> Projects { get; } = [];
    public ObservableCollection<string> ProjectFilterFields { get; } = [];
    public ObservableCollection<string> ProjectFilterValues { get; } = [];
    public ObservableCollection<DashboardTicketChoice> AdditionalTickets { get; } = [];
    public ObservableCollection<KanbanColumn> Columns { get; } = [];

    public KanbanDashboardViewModel(ITicketService ticketService, IAppSettingsService settingsService)
    {
        _ticketService = ticketService;
        _settingsService = settingsService;
        _isApplyingSettings = true;
        var settings = _settingsService.Load();
        SelectedProjectFilterField = settings.DashboardProjectFilterField;
        SelectedProjectFilterValue = settings.DashboardProjectFilterValue;
        _isApplyingSettings = false;
    }

    partial void OnSelectedProjectFilterFieldChanged(string? value)
    {
        if (_isApplyingSettings) return;
        _isApplyingSettings = true;
        SelectedProjectFilterValue = null;
        _isApplyingSettings = false;
        RebuildProjectFilterValues();
        PersistProjectFilter();
        UpdateVisibleProjects();
    }

    partial void OnSelectedProjectFilterValueChanged(string? value)
    {
        if (_isApplyingSettings) return;
        PersistProjectFilter();
        UpdateVisibleProjects();
    }

    public async Task LoadAsync()
    {
        CancelLoad();
        var cts = new CancellationTokenSource();
        _loadCts = cts;
        var settings = _settingsService.Load();
        if (string.IsNullOrWhiteSpace(settings.ApiKey) || string.IsNullOrWhiteSpace(settings.BaseUrl))
        {
            StatusMessage = "Bitte zuerst die Redmine-Verbindung in den Einstellungen konfigurieren.";
            cts.Dispose();
            _loadCts = null;
            return;
        }

        IsBusy = true;
        StatusMessage = "Projekte werden geladen …";
        try
        {
            var projectsTask = _ticketService.GetProjectsAsync(settings.BaseUrl, settings.ApiKey, cts.Token);
            var statusesTask = _ticketService.GetIssueStatusesAsync(settings.BaseUrl, settings.ApiKey, cts.Token);
            await Task.WhenAll(projectsTask, statusesTask);
            if (cts.IsCancellationRequested) return;

            _isApplyingSettings = true;
            _statuses = await statusesTask;
            var allProjects = await projectsTask;
            _allProjectChoices = allProjects.Select(project => new ProjectChoice(
                project, settings.DashboardProjectIds.Contains(project.Id))).ToList();
            foreach (var choice in _allProjectChoices)
            {
                choice.PropertyChanged += OnSelectionChanged;
            }

            RebuildProjectFilterFields();
            _isApplyingSettings = true;
            SelectedProjectFilterField = ProjectFilterFields.Contains(settings.DashboardProjectFilterField ?? string.Empty)
                ? settings.DashboardProjectFilterField
                : null;
            RebuildProjectFilterValues();
            SelectedProjectFilterValue = ProjectFilterValues.Contains(settings.DashboardProjectFilterValue ?? string.Empty)
                ? settings.DashboardProjectFilterValue
                : null;
            _isApplyingSettings = false;
            UpdateVisibleProjects();

            AdditionalTickets.Clear();
            foreach (var ticketId in settings.DashboardTicketIds.Distinct())
            {
                var ticket = await _ticketService.GetIssueByIdAsync(settings.BaseUrl, settings.ApiKey, ticketId, cts.Token);
                if (ticket is null) continue;
                var choice = new DashboardTicketChoice(ticket, true);
                choice.PropertyChanged += OnSelectionChanged;
                AdditionalTickets.Add(choice);
            }
            _isApplyingSettings = false;
            await LoadSelectedProjectTicketsAsync(settings.BaseUrl, settings.ApiKey, cts.Token);
            if (!cts.IsCancellationRequested)
            {
                StatusMessage = $"{Projects.Count} von {_allProjectChoices.Count} Projekten angezeigt · {_allTickets.Count} Tickets geladen";
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _isApplyingSettings = false;
            StatusMessage = $"Projekte oder Tickets konnten nicht geladen werden: {ex.Message}";
        }
        finally
        {
            var isCurrentLoad = _loadCts == cts;
            if (isCurrentLoad) _loadCts = null;
            cts.Dispose();
            if (isCurrentLoad) IsBusy = false;
        }
    }

    [RelayCommand]
    private Task Reload() => LoadAsync();

    public Task<IssueDto?> LoadTicketDetailsAsync(int ticketId, CancellationToken cancellationToken = default)
    {
        var settings = _settingsService.Load();
        if (string.IsNullOrWhiteSpace(settings.ApiKey) || string.IsNullOrWhiteSpace(settings.BaseUrl))
        {
            throw new InvalidOperationException("Bitte zuerst die Redmine-Verbindung in den Einstellungen konfigurieren.");
        }

        return _ticketService.GetIssueByIdAsync(settings.BaseUrl, settings.ApiKey, ticketId, cancellationToken);
    }

    [RelayCommand]
    private async Task AddTicketByIdAsync()
    {
        var ticketIdText = TicketIdToAdd.Trim().TrimStart('#');
        if (!int.TryParse(ticketIdText, NumberStyles.None, CultureInfo.InvariantCulture, out var ticketId) || ticketId <= 0)
        {
            StatusMessage = "Bitte eine gültige Ticketnummer eingeben.";
            return;
        }

        var settings = _settingsService.Load();
        IsBusy = true;
        try
        {
            var ticket = await _ticketService.GetIssueByIdAsync(settings.BaseUrl, settings.ApiKey, ticketId);
            if (ticket is null)
            {
                StatusMessage = $"Ticket #{ticketId} wurde nicht gefunden oder ist nicht zugänglich.";
                return;
            }

            var existing = AdditionalTickets.FirstOrDefault(item => item.Ticket.Id == ticket.Id);
            if (existing is null)
            {
                var choice = new DashboardTicketChoice(ticket, true);
                choice.PropertyChanged += OnSelectionChanged;
                AdditionalTickets.Add(choice);
            }
            else
            {
                existing.IsSelected = true;
            }

            TicketIdToAdd = "";
            PersistSelection();
            UpdateBoardFromLoadedData();
            StatusMessage = $"Ticket #{ticket.Id} wurde zum Teamboard hinzugefügt.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ticket #{ticketId} konnte nicht geladen werden: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnSelectionChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_isApplyingSettings || e.PropertyName != nameof(ProjectChoice.IsSelected) && e.PropertyName != nameof(DashboardTicketChoice.IsSelected)) return;
        PersistSelection();
        if (e.PropertyName == nameof(ProjectChoice.IsSelected))
        {
            _ = ReloadSelectedProjectTicketsAsync();
        }
        else
        {
            UpdateBoardFromLoadedData();
        }
    }

    private void PersistSelection()
    {
        _settingsService.Update(settings =>
        {
            settings.DashboardProjectIds = _allProjectChoices.Where(project => project.IsSelected).Select(project => project.Id).ToList();
            settings.DashboardTicketIds = AdditionalTickets.Where(ticket => ticket.IsSelected).Select(ticket => ticket.Ticket.Id).ToList();
        });
    }

    private void PersistProjectFilter() => _settingsService.Update(settings =>
    {
        settings.DashboardProjectFilterField = SelectedProjectFilterField;
        settings.DashboardProjectFilterValue = SelectedProjectFilterValue;
    });

    private void RebuildProjectFilterFields()
    {
        ProjectFilterFields.Clear();
        foreach (var field in _allProjectChoices
                     .SelectMany(choice => GetMetadata(choice.Project).Keys)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(field => field, StringComparer.CurrentCultureIgnoreCase))
        {
            ProjectFilterFields.Add(field);
        }
        RebuildProjectFilterValues();
    }

    private void RebuildProjectFilterValues()
    {
        ProjectFilterValues.Clear();
        if (string.IsNullOrWhiteSpace(SelectedProjectFilterField)) return;

        foreach (var value in _allProjectChoices
                     .Select(choice => GetMetadata(choice.Project))
                     .Where(metadata => metadata.TryGetValue(SelectedProjectFilterField, out _))
                     .Select(metadata => metadata[SelectedProjectFilterField])
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase))
        {
            ProjectFilterValues.Add(value);
        }
    }

    private void UpdateVisibleProjects()
    {
        Projects.Clear();
        IEnumerable<ProjectChoice> visible = _allProjectChoices;
        if (!string.IsNullOrWhiteSpace(SelectedProjectFilterField) &&
            !string.IsNullOrWhiteSpace(SelectedProjectFilterValue))
        {
            visible = visible.Where(choice => GetMetadata(choice.Project)
                .TryGetValue(SelectedProjectFilterField, out var value) &&
                string.Equals(value, SelectedProjectFilterValue, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var choice in visible) Projects.Add(choice);
    }

    private static Dictionary<string, string> GetMetadata(ProjectDto project)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Projektname"] = project.Name
        };
        if (!string.IsNullOrWhiteSpace(project.Identifier)) metadata["Projektkennung"] = project.Identifier;
        metadata["Redmine-Status"] = project.Status switch
        {
            1 => "Aktiv",
            5 => "Geschlossen",
            9 => "Archiviert",
            _ => $"Status {project.Status}"
        };
        metadata["Öffentlich"] = project.Is_Public ? "ja" : "nein";
        if (!string.IsNullOrWhiteSpace(project.Parent?.Name)) metadata["Übergeordnetes Projekt"] = project.Parent.Name;
        foreach (var field in project.Custom_Fields)
        {
            var value = field.GetDisplayValue();
            if (!string.IsNullOrWhiteSpace(field.Name) && !string.IsNullOrWhiteSpace(value)) metadata[field.Name] = value;
        }
        return metadata;
    }

    [RelayCommand]
    private void ClearProjectFilter()
    {
        _isApplyingSettings = true;
        SelectedProjectFilterField = null;
        SelectedProjectFilterValue = null;
        _isApplyingSettings = false;
        RebuildProjectFilterValues();
        PersistProjectFilter();
        UpdateVisibleProjects();
    }

    private async Task ReloadSelectedProjectTicketsAsync()
    {
        CancelLoad();
        var cts = new CancellationTokenSource();
        _loadCts = cts;
        var settings = _settingsService.Load();
        IsBusy = true;
        StatusMessage = "Tickets der ausgewählten Projekte werden geladen …";
        try
        {
            await LoadSelectedProjectTicketsAsync(settings.BaseUrl, settings.ApiKey, cts.Token);
            if (!cts.IsCancellationRequested)
            {
                StatusMessage = $"{SelectedIssues(_allTickets).Count} Tickets im Teamboard · {_allTickets.Count} geladen";
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            StatusMessage = $"Tickets der ausgewählten Projekte konnten nicht geladen werden: {ex.Message}";
        }
        finally
        {
            var isCurrentLoad = _loadCts == cts;
            if (isCurrentLoad) _loadCts = null;
            cts.Dispose();
            if (isCurrentLoad) IsBusy = false;
        }
    }

    private async Task LoadSelectedProjectTicketsAsync(string baseUrl, string apiKey, CancellationToken cancellationToken)
    {
        var projectIds = _allProjectChoices.Where(project => project.IsSelected).Select(project => project.Id).ToList();
        var ticketGroups = await Task.WhenAll(projectIds.Select(projectId =>
            _ticketService.GetProjectTicketsAsync(baseUrl, apiKey, projectId, cancellationToken)));
        if (cancellationToken.IsCancellationRequested) return;

        _allTickets = ticketGroups.SelectMany(group => group)
            .Concat(AdditionalTickets.Where(ticket => ticket.IsSelected).Select(ticket => ticket.Ticket))
            .DistinctBy(ticket => ticket.Id)
            .OrderBy(ticket => ticket.Project?.Name)
            .ThenBy(ticket => ticket.Id)
            .ToList();
        UpdateBoardFromLoadedData();
    }

    private void UpdateBoardFromLoadedData()
    {
        var selected = SelectedIssues(_allTickets);
        Columns.Clear();
        var orderedStatuses = _statuses.OrderBy(status => status.Is_Closed).ThenBy(status => status.Name).ToList();
        foreach (var status in orderedStatuses)
        {
            Columns.Add(new KanbanColumn(status.Name, selected.Where(ticket => ticket.Status?.Id == status.Id).ToList()));
        }
        foreach (var group in selected.Where(ticket => ticket.Status is null || orderedStatuses.All(status => status.Id != ticket.Status.Id))
                     .GroupBy(ticket => ticket.Status?.Name ?? "Unbekannt").OrderBy(group => group.Key))
        {
            Columns.Add(new KanbanColumn(group.Key, group.ToList()));
        }
    }

    private List<IssueDto> SelectedIssues(IEnumerable<IssueDto> tickets)
    {
        var projectIds = _allProjectChoices.Where(project => project.IsSelected).Select(project => project.Id).ToHashSet();
        var ticketIds = AdditionalTickets.Where(item => item.IsSelected).Select(item => item.Ticket.Id).ToHashSet();
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
    public ProjectDto Project { get; }
    public int Id => Project.Id;
    public string Name => Project.Name;
    [ObservableProperty] private bool isSelected;
    public ProjectChoice(ProjectDto project, bool selected) { Project = project; IsSelected = selected; }
}

public partial class DashboardTicketChoice : ObservableObject
{
    public IssueDto Ticket { get; }
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
