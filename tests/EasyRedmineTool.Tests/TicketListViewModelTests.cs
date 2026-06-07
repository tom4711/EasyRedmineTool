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

        public void InvalidateTimeEntryCache()
        {
        }

        public Task<IReadOnlyList<IssueDto>> GetMyOpenIssuesAsync(
            string baseUrl,
            string apiKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<IssueDto>>([]);

        public async Task<TicketListLoadResult> GetTicketsForListAsync(
            string baseUrl,
            string apiKey,
            CancellationToken cancellationToken = default)
        {
            _loadCallCount++;
            if (_loadCallCount == 1 && FirstCallDelay > TimeSpan.Zero)
            {
                await Task.Delay(FirstCallDelay, cancellationToken);
            }

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
