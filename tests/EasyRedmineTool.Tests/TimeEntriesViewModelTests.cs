namespace EasyRedmineTool.Tests;

using EasyRedmineTool.Core.Api;
using EasyRedmineTool.Core;
using EasyRedmineTool.Core.Configuration;
using EasyRedmineTool.Core.Models.TimeEntries;
using EasyRedmineTool.Core.Models.Tickets;
using EasyRedmineTool.Core.Services;
using EasyRedmineTool.Core.Services.Interfaces;
using EasyRedmineTool.Core.ViewModels;

using Microsoft.Extensions.Logging.Abstractions;

public class TimeEntriesViewModelTests
{
    [Fact]
    public void PrepareForIssue_focuses_existing_favorite()
    {
        using var context = TestContext.Create();
        context.SettingsService.Save(new AppSettings
        {
            BaseUrl = "https://redmine.example/",
            ApiKey = "secret",
            FavoriteTicketIds = [100, 200],
            CachedTickets =
            [
                new IssueDto { Id = 100, Subject = "Alpha" },
                new IssueDto { Id = 200, Subject = "Beta" }
            ]
        });

        var viewModel = new TimeEntriesViewModel(context.SettingsService, context.TimeEntryService);
        viewModel.PrepareForIssue(200);

        Assert.Equal(200, viewModel.FocusedIssueId);
        Assert.Single(viewModel.FilteredFavoriteRows);
        Assert.Equal(200, viewModel.FilteredFavoriteRows[0].Ticket.Id);
        Assert.True(viewModel.FilteredFavoriteRows[0].IsFocused);
    }

    [Fact]
    public void PrepareForIssue_shows_message_for_non_favorite()
    {
        using var context = TestContext.Create();
        context.SettingsService.Save(new AppSettings
        {
            BaseUrl = "https://redmine.example/",
            ApiKey = "secret",
            FavoriteTicketIds = [100],
            CachedTickets = [new IssueDto { Id = 100, Subject = "Alpha" }]
        });

        var viewModel = new TimeEntriesViewModel(context.SettingsService, context.TimeEntryService);
        viewModel.PrepareForIssue(999);

        Assert.Null(viewModel.FocusedIssueId);
        Assert.Contains("kein Favorit", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FavoriteFilterText_filters_by_subject()
    {
        using var context = TestContext.Create();
        context.SettingsService.Save(new AppSettings
        {
            BaseUrl = "https://redmine.example/",
            ApiKey = "secret",
            FavoriteTicketIds = [100, 200],
            CachedTickets =
            [
                new IssueDto { Id = 100, Subject = "Alpha Task" },
                new IssueDto { Id = 200, Subject = "Beta Task" }
            ]
        });

        var viewModel = new TimeEntriesViewModel(context.SettingsService, context.TimeEntryService);
        viewModel.FavoriteFilterText = "beta";

        Assert.Single(viewModel.FilteredFavoriteRows);
        Assert.Equal(200, viewModel.FilteredFavoriteRows[0].Ticket.Id);
    }

    [Fact]
    public async Task ReloadTodayBookedHoursAsync_sums_today_entries()
    {
        using var context = TestContext.Create();
        context.SettingsService.Save(new AppSettings
        {
            BaseUrl = "https://redmine.example/",
            ApiKey = "secret"
        });
        context.TimeEntryService.Entries =
        [
            new TimeEntryDto { Spent_On = RedmineDates.FormatSpentOn(DateTime.Today), Hours = 2.5 },
            new TimeEntryDto { Spent_On = RedmineDates.FormatSpentOn(DateTime.Today), Hours = 1.25 },
            new TimeEntryDto { Spent_On = RedmineDates.FormatSpentOn(DateTime.Today.AddDays(-1)), Hours = 8 }
        ];

        var viewModel = new TimeEntriesViewModel(context.SettingsService, context.TimeEntryService);
        await viewModel.ReloadTodayBookedHoursAsync();

        Assert.Equal(3.75, viewModel.TodayBookedHours);
    }

    private sealed class TestContext : IDisposable
    {
        private readonly string _settingsPath;

        private TestContext(string settingsPath, AppSettingsService settingsService, FakeTimeEntryService timeEntryService)
        {
            _settingsPath = settingsPath;
            SettingsService = settingsService;
            TimeEntryService = timeEntryService;
        }

        public AppSettingsService SettingsService { get; }
        public FakeTimeEntryService TimeEntryService { get; }

        public static TestContext Create()
        {
            var settingsPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
            var settingsService = new AppSettingsService(settingsPath, NullLogger<AppSettingsService>.Instance);
            return new TestContext(settingsPath, settingsService, new FakeTimeEntryService());
        }

        public void Dispose()
        {
            if (File.Exists(_settingsPath))
            {
                File.Delete(_settingsPath);
            }

            var tempPath = _settingsPath + ".tmp";
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private sealed class FakeTimeEntryService : ITimeEntryService
    {
        public IReadOnlyList<TimeEntryDto> Entries { get; set; } = [];

        public Task<IReadOnlyList<TimeEntryActivityDto>> GetActivitiesAsync(
            string baseUrl,
            string apiKey,
            int? issueId = null,
            int? projectId = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TimeEntryActivityDto>>([]);

        public Task<TimeEntryLoadResult> GetMyTimeEntriesAsync(
            string baseUrl,
            string apiKey,
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new TimeEntryLoadResult
            {
                Success = true,
                Entries = Entries
            });

        public Task<TimeEntryOperationResult> CreateTimeEntryAsync(
            string baseUrl,
            string apiKey,
            TimeEntryCreateRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new TimeEntryOperationResult { Success = true });
    }
}
