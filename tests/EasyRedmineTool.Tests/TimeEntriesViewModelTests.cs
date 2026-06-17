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
    public void PrepareForIssue_switches_to_all_tickets_for_non_favorite()
    {
        using var context = TestContext.Create();
        context.SettingsService.Save(new AppSettings
        {
            BaseUrl = "https://redmine.example/",
            ApiKey = "secret",
            FavoriteTicketIds = [100],
            CachedTickets =
            [
                new IssueDto { Id = 100, Subject = "Alpha" },
                new IssueDto { Id = 200, Subject = "Beta" }
            ]
        });

        var viewModel = new TimeEntriesViewModel(context.SettingsService, context.TimeEntryService);
        viewModel.PrepareForIssue(200);

        Assert.False(viewModel.ShowFavoritesOnly);
        Assert.Equal(200, viewModel.FocusedIssueId);
        Assert.Single(viewModel.FilteredFavoriteRows);
        Assert.Equal(200, viewModel.FilteredFavoriteRows[0].Ticket.Id);
        Assert.True(viewModel.FilteredFavoriteRows[0].IsFocused);
    }

    [Fact]
    public void PrepareForIssue_shows_message_for_missing_ticket()
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
        Assert.Contains("nicht in der lokalen Ticketliste", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShowAllTicketsView_lists_all_cached_tickets()
    {
        using var context = TestContext.Create();
        context.SettingsService.Save(new AppSettings
        {
            BaseUrl = "https://redmine.example/",
            ApiKey = "secret",
            FavoriteTicketIds = [100],
            CachedTickets =
            [
                new IssueDto { Id = 100, Subject = "Alpha" },
                new IssueDto { Id = 200, Subject = "Beta" },
                new IssueDto { Id = 300, Subject = "Gamma" }
            ]
        });

        var viewModel = new TimeEntriesViewModel(context.SettingsService, context.TimeEntryService);
        viewModel.ReloadFavorites();
        Assert.Single(viewModel.FavoriteRows);

        viewModel.ShowAllTicketsViewCommand.Execute(null);

        Assert.False(viewModel.ShowFavoritesOnly);
        Assert.Equal(3, viewModel.FavoriteRows.Count);
        Assert.Equal(3, viewModel.FilteredFavoriteRows.Count);
    }

    [Fact]
    public void FavoriteFilterText_searches_only_within_favorites_scope()
    {
        using var context = TestContext.Create();
        context.SettingsService.Save(new AppSettings
        {
            BaseUrl = "https://redmine.example/",
            ApiKey = "secret",
            FavoriteTicketIds = [100],
            CachedTickets =
            [
                new IssueDto { Id = 100, Subject = "Alpha Task" },
                new IssueDto { Id = 200, Subject = "Beta Task" }
            ]
        });

        var viewModel = new TimeEntriesViewModel(context.SettingsService, context.TimeEntryService);
        viewModel.ReloadFavorites();
        viewModel.FavoriteFilterText = "beta";

        Assert.Empty(viewModel.FilteredFavoriteRows);
        Assert.Equal("0 von 1 Favoriten", viewModel.ListScopeSummary);

        viewModel.ShowAllTicketsViewCommand.Execute(null);
        viewModel.FavoriteFilterText = "beta";

        Assert.Single(viewModel.FilteredFavoriteRows);
        Assert.Equal(200, viewModel.FilteredFavoriteRows[0].Ticket.Id);
        Assert.Equal("1 von 2 Tickets", viewModel.ListScopeSummary);
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
        viewModel.ReloadFavorites();
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
        Assert.Equal(2, viewModel.TodayTimeEntries.Count);
        Assert.True(viewModel.HasTodayTimeEntries);
    }

    [Fact]
    public async Task ReloadTodayBookedHoursAsync_populates_today_entry_rows()
    {
        using var context = TestContext.Create();
        context.SettingsService.Save(new AppSettings
        {
            BaseUrl = "https://redmine.example/",
            ApiKey = "secret",
            CachedTickets = [new IssueDto { Id = 42, Subject = "Support Task" }]
        });
        context.TimeEntryService.Entries =
        [
            new TimeEntryDto
            {
                Id = 10,
                Issue_Id = 42,
                Spent_On = RedmineDates.FormatSpentOn(DateTime.Today),
                Hours = 1.5,
                Activity = new TimeEntryActivityDto { Id = 3, Name = "Entwicklung" },
                Comments = "Fix"
            }
        ];

        var viewModel = new TimeEntriesViewModel(context.SettingsService, context.TimeEntryService);
        await viewModel.ReloadTodayBookedHoursAsync();

        Assert.Single(viewModel.TodayTimeEntries);
        Assert.Equal(10, viewModel.TodayTimeEntries[0].EntryId);
        Assert.Contains("Support Task", viewModel.TodayTimeEntries[0].TicketLabel, StringComparison.Ordinal);
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

        public bool LoadSuccess { get; set; } = true;

        public Task<IReadOnlyList<TimeEntryActivityDto>> GetActivitiesAsync(
            string baseUrl,
            string apiKey,
            int? issueId = null,
            int? projectId = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TimeEntryActivityDto>>([]);

        public Task<IReadOnlyList<TimeEntryCustomFieldValueDto>> GetRecentCustomFieldValuesAsync(
            string baseUrl,
            string apiKey,
            int? issueId = null,
            int? projectId = null,
            int? activityId = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TimeEntryCustomFieldValueDto>>([]);

        public Task<IReadOnlyList<TimeEntryCustomFieldRowViewModel>> GetCustomFieldRowsAsync(
            AppSettings settings,
            int? issueId = null,
            int? projectId = null,
            int? activityId = null,
            IReadOnlyList<TimeEntryCustomFieldValueDto>? existingValues = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(TimeEntryCustomFieldSupport.CreateRows([], [], settings, projectId, activityId, existingValues));

        public Task<TimeEntryLoadResult> GetMyTimeEntriesAsync(
            string baseUrl,
            string apiKey,
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(LoadSuccess
                ? new TimeEntryLoadResult { Success = true, Entries = Entries }
                : new TimeEntryLoadResult { Success = false, Message = "load failed" });

        public Task<TimeEntryOperationResult> CreateTimeEntryAsync(
            string baseUrl,
            string apiKey,
            TimeEntryCreateRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new TimeEntryOperationResult { Success = true });

        public Task<TimeEntryOperationResult> UpdateTimeEntryAsync(
            string baseUrl,
            string apiKey,
            int timeEntryId,
            TimeEntryUpdateRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new TimeEntryOperationResult { Success = true });

        public Task<TimeEntryOperationResult> DeleteTimeEntryAsync(
            string baseUrl,
            string apiKey,
            int timeEntryId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new TimeEntryOperationResult { Success = true });
    }
}
