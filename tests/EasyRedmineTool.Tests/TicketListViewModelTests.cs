namespace EasyRedmineTool.Tests;

using EasyRedmineTool.Core.Configuration;
using EasyRedmineTool.Core.Models.Tickets;
using EasyRedmineTool.Core.Services;
using EasyRedmineTool.Core.Services.Interfaces;
using EasyRedmineTool.Core.ViewModels;

using Microsoft.Extensions.Logging.Abstractions;

public class TicketListViewModelTests
{
    [Fact]
    public void TicketFilterText_filters_displayed_tickets_by_subject_and_id()
    {
        using var context = TestContext.Create();
        context.SettingsService.Save(new AppSettings
        {
            BaseUrl = "https://redmine.example/",
            ApiKey = "secret",
            CachedTickets =
            [
                new IssueDto { Id = 10, Subject = "Alpha task", Project = new NamedEntityDto { Name = "Proj A" } },
                new IssueDto { Id = 20, Subject = "Beta task", Project = new NamedEntityDto { Name = "Proj B" } }
            ],
            LastLoadedTicketIds = [10, 20]
        });

        var viewModel = new TicketListViewModel(context.TicketService, context.SettingsService);

        Assert.Equal(2, viewModel.Tickets.Count);
        Assert.Equal(2, viewModel.FilteredTickets.Count);

        viewModel.TicketFilterText = "beta";

        Assert.Equal(2, viewModel.Tickets.Count);
        Assert.Single(viewModel.FilteredTickets);
        Assert.Equal(20, viewModel.FilteredTickets[0].Ticket.Id);
        Assert.Equal("1 von 2 Tickets", viewModel.ListScopeSummary);

        viewModel.TicketFilterText = "10";

        Assert.Single(viewModel.FilteredTickets);
        Assert.Equal(10, viewModel.FilteredTickets[0].Ticket.Id);

        viewModel.TicketFilterText = "proj b";

        Assert.Single(viewModel.FilteredTickets);
        Assert.Equal(20, viewModel.FilteredTickets[0].Ticket.Id);

        viewModel.ToggleFavoriteForTicketCommand.Execute(viewModel.FilteredTickets[0]);

        viewModel.TicketFilterText = string.Empty;

        Assert.Equal(2, viewModel.FilteredTickets.Count);
        Assert.Contains(viewModel.FilteredTickets, ticket => ticket.Ticket.Id == 20 && ticket.IsFavorite);
        Assert.Equal("2 Tickets", viewModel.ListScopeSummary);
    }

    [Fact]
    public void TicketFilterText_handles_null_subject_without_throwing()
    {
        using var context = TestContext.Create();
        context.SettingsService.Save(new AppSettings
        {
            BaseUrl = "https://redmine.example/",
            ApiKey = "secret",
            CachedTickets = [new IssueDto { Id = 30, Subject = null! }],
            LastLoadedTicketIds = [30]
        });

        var viewModel = new TicketListViewModel(context.TicketService, context.SettingsService);

        viewModel.TicketFilterText = "missing";

        Assert.Empty(viewModel.FilteredTickets);
        Assert.Equal("0 von 1 Tickets", viewModel.ListScopeSummary);

        viewModel.TicketFilterText = "30";

        Assert.Single(viewModel.FilteredTickets);
        Assert.Equal(30, viewModel.FilteredTickets[0].Ticket.Id);
    }

    [Fact]
    public async Task LoadTicketsAsync_keeps_filtered_tickets_in_sync_when_load_fails()
    {
        using var context = TestContext.Create();
        context.SettingsService.Save(new AppSettings
        {
            BaseUrl = "https://redmine.example/",
            ApiKey = "secret",
            CachedTickets = [new IssueDto { Id = 10, Subject = "Cached" }],
            LastLoadedTicketIds = [10]
        });
        context.TicketService.NextLoadException = new InvalidOperationException("API down");

        var viewModel = new TicketListViewModel(context.TicketService, context.SettingsService);
        viewModel.TicketFilterText = "cached";

        Assert.Single(viewModel.FilteredTickets);

        await viewModel.LoadTicketsCommand.ExecuteAsync(null);

        Assert.Empty(viewModel.Tickets);
        Assert.Empty(viewModel.FilteredTickets);
        Assert.Equal("Keine Tickets geladen", viewModel.ListScopeSummary);
        Assert.Contains("API down", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadTicketsAsync_populates_ticket_list_from_service()
    {
        using var context = TestContext.Create();
        context.SettingsService.Save(new AppSettings
        {
            BaseUrl = "https://redmine.example/",
            ApiKey = "secret"
        });
        context.TicketService.NextLoadResult = new TicketListLoadResult
        {
            Tickets = [new IssueDto { Id = 10, Subject = "Loaded Ticket" }],
            OpenTicketCount = 1,
            TimeEntryTicketCount = 0
        };

        var viewModel = new TicketListViewModel(context.TicketService, context.SettingsService);
        await viewModel.LoadTicketsCommand.ExecuteAsync(null);

        Assert.Single(viewModel.Tickets);
        Assert.Equal(10, viewModel.Tickets[0].Ticket.Id);
        Assert.Contains("1 Ticket(s) geladen", viewModel.StatusMessage, StringComparison.Ordinal);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task AddTicketByIdAsync_adds_ticket_when_not_present()
    {
        using var context = TestContext.Create();
        context.SettingsService.Save(new AppSettings
        {
            BaseUrl = "https://redmine.example/",
            ApiKey = "secret"
        });
        context.TicketService.NextIssue = new IssueDto { Id = 42, Subject = "Added Ticket" };

        var viewModel = new TicketListViewModel(context.TicketService, context.SettingsService)
        {
            TicketIdToAdd = "42"
        };

        await viewModel.AddTicketByIdCommand.ExecuteAsync(null);

        Assert.Single(viewModel.Tickets);
        Assert.Equal(42, viewModel.Tickets[0].Ticket.Id);
        Assert.Equal(string.Empty, viewModel.TicketIdToAdd);
        Assert.Contains("#42 wurde hinzugefügt", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadTicketsAsync_preserves_favorited_tickets_in_settings_cache()
    {
        using var context = TestContext.Create();
        context.SettingsService.Save(new AppSettings
        {
            BaseUrl = "https://redmine.example/",
            ApiKey = "secret",
            CachedTickets = [new IssueDto { Id = 1, Subject = "Favorite" }],
            FavoriteTicketIds = [1],
            LastLoadedTicketIds = [1]
        });

        context.TicketService.NextLoadResult = new TicketListLoadResult
        {
            Tickets = [new IssueDto { Id = 2, Subject = "New load" }],
            OpenTicketCount = 1,
            TimeEntryTicketCount = 0
        };

        var viewModel = new TicketListViewModel(context.TicketService, context.SettingsService);

        await viewModel.LoadTicketsCommand.ExecuteAsync(null);

        var settings = context.SettingsService.Load();
        Assert.Equal([2], settings.LastLoadedTicketIds);
        Assert.Equal(2, settings.CachedTickets.Count);
        Assert.Contains(settings.CachedTickets, ticket => ticket.Id == 1);
        Assert.Contains(settings.CachedTickets, ticket => ticket.Id == 2);
        Assert.Single(viewModel.Tickets);
        Assert.Equal(2, viewModel.Tickets[0].Ticket.Id);
    }

    [Fact]
    public async Task LoadTicketsAsync_passes_selected_filters_to_service()
    {
        using var context = TestContext.Create();
        context.SettingsService.Save(new AppSettings
        {
            BaseUrl = "https://redmine.example/",
            ApiKey = "secret"
        });
        context.TicketService.NextLoadResult = new TicketListLoadResult
        {
            Tickets = [],
            OpenTicketCount = 0,
            TimeEntryTicketCount = 0
        };

        var viewModel = new TicketListViewModel(context.TicketService, context.SettingsService);
        viewModel.SelectedAssigneeFilter = viewModel.AssigneeFilterOptions.First(option => option.Value == TicketAssigneeFilter.Unassigned);
        viewModel.SelectedStatusFilter = viewModel.StatusFilterOptions.First(option => option.Value.Kind == TicketStatusFilterKind.Specific);
        viewModel.IncludeTimeEntryTickets = true;
        viewModel.TimeEntryLookbackMonths = 6;

        await viewModel.LoadTicketsCommand.ExecuteAsync(null);

        Assert.NotNull(context.TicketService.LastFilter);
        Assert.Equal(TicketAssigneeFilter.Unassigned, context.TicketService.LastFilter!.Assignee);
        Assert.Equal(TicketStatusFilterKind.Specific, context.TicketService.LastFilter.StatusKind);
        Assert.Equal(3, context.TicketService.LastFilter.StatusId);
        Assert.True(context.TicketService.LastFilter.IncludeTimeEntryTickets);
        Assert.Equal(6, context.TicketService.LastFilter.TimeEntryLookbackMonths);
    }

    [Fact]
    public async Task ReloadSettings_preserves_saved_status_filter_when_time_entry_options_are_enabled()
    {
        using var context = TestContext.Create();
        context.SettingsService.Save(new AppSettings
        {
            BaseUrl = "https://redmine.example/",
            ApiKey = "secret",
            TicketLoadAssigneeFilter = TicketAssigneeFilter.Unassigned,
            TicketLoadStatusFilterKind = TicketStatusFilterKind.Specific,
            TicketLoadStatusId = 3,
            TicketLoadStatusName = "In Arbeit",
            TicketLoadIncludeTimeEntryTickets = true,
            TicketLoadTimeEntryLookbackMonths = 6
        });

        var viewModel = new TicketListViewModel(context.TicketService, context.SettingsService);
        await Task.Delay(50);

        var settings = context.SettingsService.Load();
        Assert.Equal(TicketStatusFilterKind.Specific, settings.TicketLoadStatusFilterKind);
        Assert.Equal(3, settings.TicketLoadStatusId);
        Assert.Equal("In Arbeit", settings.TicketLoadStatusName);
        Assert.True(settings.TicketLoadIncludeTimeEntryTickets);
        Assert.Equal(6, settings.TicketLoadTimeEntryLookbackMonths);
        Assert.Equal(TicketStatusFilterKind.Specific, viewModel.SelectedStatusFilter?.Value.Kind);
        Assert.Equal(3, viewModel.SelectedStatusFilter?.Value.StatusId);

        viewModel.ReloadSettings();
        await Task.Delay(50);

        settings = context.SettingsService.Load();
        Assert.Equal(TicketStatusFilterKind.Specific, settings.TicketLoadStatusFilterKind);
        Assert.Equal(3, settings.TicketLoadStatusId);
        Assert.Equal(TicketStatusFilterKind.Specific, viewModel.SelectedStatusFilter?.Value.Kind);
        Assert.Equal(3, viewModel.SelectedStatusFilter?.Value.StatusId);
    }

    [Fact]
    public async Task LoadTicketsAsync_keeps_selected_status_filter_after_loading()
    {
        using var context = TestContext.Create();
        context.SettingsService.Save(new AppSettings
        {
            BaseUrl = "https://redmine.example/",
            ApiKey = "secret"
        });
        context.TicketService.NextLoadResult = new TicketListLoadResult
        {
            Tickets = [],
            OpenTicketCount = 0,
            TimeEntryTicketCount = 0
        };

        var viewModel = new TicketListViewModel(context.TicketService, context.SettingsService);
        await Task.Delay(50);

        var selected = viewModel.StatusFilterOptions.First(option => option.Value.StatusId == 3);
        viewModel.SelectedStatusFilter = selected;

        await viewModel.LoadTicketsCommand.ExecuteAsync(null);

        Assert.Equal(TicketStatusFilterKind.Specific, viewModel.SelectedStatusFilter?.Value.Kind);
        Assert.Equal(3, viewModel.SelectedStatusFilter?.Value.StatusId);
        Assert.Same(selected, viewModel.SelectedStatusFilter);
    }

    [Fact]
    public async Task LoadTicketsAsync_uses_latest_request_when_called_twice()
    {
        using var context = TestContext.Create();
        context.SettingsService.Save(new AppSettings
        {
            BaseUrl = "https://redmine.example/",
            ApiKey = "secret"
        });
        context.TicketService.FirstCallDelay = TimeSpan.FromMilliseconds(150);
        context.TicketService.NextLoadResult = new TicketListLoadResult
        {
            Tickets = [new IssueDto { Id = 1, Subject = "Old" }],
            OpenTicketCount = 1
        };
        context.TicketService.LatestLoadResult = new TicketListLoadResult
        {
            Tickets = [new IssueDto { Id = 2, Subject = "New" }],
            OpenTicketCount = 1
        };

        var viewModel = new TicketListViewModel(context.TicketService, context.SettingsService);

        var firstCall = viewModel.LoadTicketsCommand.ExecuteAsync(null);
        await viewModel.LoadTicketsCommand.ExecuteAsync(null);
        await firstCall;

        Assert.Single(viewModel.Tickets);
        Assert.Equal(2, viewModel.Tickets[0].Ticket.Id);
        Assert.False(viewModel.IsBusy);
    }

    private sealed class TestContext : IDisposable
    {
        private readonly string _settingsPath;

        private TestContext(string settingsPath, AppSettingsService settingsService, DelayedTicketService ticketService)
        {
            _settingsPath = settingsPath;
            SettingsService = settingsService;
            TicketService = ticketService;
        }

        public AppSettingsService SettingsService { get; }
        public DelayedTicketService TicketService { get; }

        public static TestContext Create()
        {
            var settingsPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
            var settingsService = new AppSettingsService(settingsPath, NullLogger<AppSettingsService>.Instance);
            return new TestContext(settingsPath, settingsService, new DelayedTicketService());
        }

        public void Dispose()
        {
            if (File.Exists(_settingsPath))
            {
                File.Delete(_settingsPath);
            }
        }
    }

    private sealed class DelayedTicketService : ITicketService
    {
        private int _loadCallCount;

        public TimeSpan FirstCallDelay { get; set; }
        public TicketListLoadResult NextLoadResult { get; set; } = new();
        public TicketListLoadResult LatestLoadResult { get; set; } = new();
        public IssueDto? NextIssue { get; set; }
        public Exception? NextLoadException { get; set; }

        public void InvalidateTimeEntryCache()
        {
        }

        public IReadOnlyList<StatusDto> NextStatuses { get; set; } =
        [
            new StatusDto { Id = 1, Name = "Neu", Is_Closed = false },
            new StatusDto { Id = 3, Name = "In Arbeit", Is_Closed = false },
            new StatusDto { Id = 5, Name = "Erledigt", Is_Closed = true }
        ];

        public Task<IReadOnlyList<IssueDto>> GetMyOpenIssuesAsync(
            string baseUrl,
            string apiKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<IssueDto>>([]);

        public Task<IReadOnlyList<StatusDto>> GetIssueStatusesAsync(
            string baseUrl,
            string apiKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(NextStatuses);

        public TicketLoadFilter? LastFilter { get; private set; }

        public async Task<TicketListLoadResult> GetTicketsForListAsync(
            string baseUrl,
            string apiKey,
            TicketLoadFilter filter,
            CancellationToken cancellationToken = default)
        {
            _loadCallCount++;
            if (NextLoadException is not null)
            {
                throw NextLoadException;
            }

            if (_loadCallCount == 1 && FirstCallDelay > TimeSpan.Zero)
            {
                await Task.Delay(FirstCallDelay, cancellationToken);
            }

            LastFilter = filter;
            cancellationToken.ThrowIfCancellationRequested();
            return _loadCallCount == 1 ? NextLoadResult : LatestLoadResult;
        }

        public Task<IssueDto?> GetIssueByIdAsync(
            string baseUrl,
            string apiKey,
            int issueId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(NextIssue);
    }
}
