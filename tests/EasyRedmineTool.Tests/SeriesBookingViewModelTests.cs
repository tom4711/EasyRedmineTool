namespace EasyRedmineTool.Tests;

using EasyRedmineTool.Core;
using EasyRedmineTool.Core.Configuration;
using EasyRedmineTool.Core.Models.TimeEntries;
using EasyRedmineTool.Core.Models.Tickets;
using EasyRedmineTool.Core.Services;
using EasyRedmineTool.Core.Services.Interfaces;
using EasyRedmineTool.Core.ViewModels;

using Microsoft.Extensions.Logging.Abstractions;

public class SeriesBookingViewModelTests
{
    [Fact]
    public void PrepareView_loads_cached_tickets_with_favorites_first()
    {
        using var context = TestContext.Create();
        context.SettingsService.Save(new AppSettings
        {
            BaseUrl = "https://redmine.example/",
            ApiKey = "secret",
            FavoriteTicketIds = [200],
            CachedTickets =
            [
                new IssueDto { Id = 100, Subject = "Alpha" },
                new IssueDto { Id = 200, Subject = "Beta" }
            ]
        });

        var viewModel = new SeriesBookingViewModel(context.SettingsService, context.TimeEntryService);
        viewModel.PrepareView();

        Assert.Single(viewModel.Tickets);
        Assert.Equal(200, viewModel.Tickets[0].Id);
        Assert.Equal(200, viewModel.SelectedTicket?.Id);

        viewModel.ShowAllTicketsViewCommand.Execute(null);

        Assert.Equal(2, viewModel.Tickets.Count);
        Assert.Equal(200, viewModel.Tickets[0].Id);
    }

    [Fact]
    public async Task RefreshPreviewAsync_marks_conflicts_and_deselects_them()
    {
        using var context = TestContext.Create();
        var monday = GetPreviousMonday(DateTime.Today);
        var tuesday = monday.AddDays(1);

        context.SettingsService.Save(new AppSettings
        {
            BaseUrl = "https://redmine.example/",
            ApiKey = "secret",
            FavoriteTicketIds = [42],
            CachedTickets = [new IssueDto { Id = 42, Subject = "Task" }]
        });

        context.TimeEntryService.Entries =
        [
            new TimeEntryDto
            {
                Issue_Id = 42,
                Spent_On = RedmineDates.FormatSpentOn(tuesday),
                Hours = 1
            }
        ];

        var viewModel = new SeriesBookingViewModel(context.SettingsService, context.TimeEntryService);
        viewModel.PrepareView();
        viewModel.FromDate = monday;
        viewModel.ToDate = tuesday;
        viewModel.Hours = "2";
        viewModel.SelectedActivity = new TimeEntryActivityDto { Id = 1, Name = "Dev" };
        viewModel.Activities.Add(viewModel.SelectedActivity);

        await viewModel.RefreshPreviewCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasPreview);
        Assert.Equal(2, viewModel.PreviewRows.Count);
        Assert.Single(viewModel.PreviewRows, row => row.HasConflict);
        Assert.Equal(1, viewModel.PreviewRows.Count(row => row.IsSelected));
        Assert.Contains("1 Konflikt", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConfirmBookSeriesAsync_creates_entries_for_selected_days()
    {
        using var context = TestContext.Create();
        var monday = GetPreviousMonday(DateTime.Today);

        context.SettingsService.Save(new AppSettings
        {
            BaseUrl = "https://redmine.example/",
            ApiKey = "secret",
            FavoriteTicketIds = [42],
            CachedTickets = [new IssueDto { Id = 42, Subject = "Task" }]
        });

        var viewModel = new SeriesBookingViewModel(context.SettingsService, context.TimeEntryService);
        viewModel.PrepareView();
        viewModel.FromDate = monday;
        viewModel.ToDate = monday;
        viewModel.Hours = "2";
        viewModel.SelectedActivity = new TimeEntryActivityDto { Id = 3, Name = "Dev" };
        viewModel.Activities.Add(viewModel.SelectedActivity);

        await viewModel.RefreshPreviewCommand.ExecuteAsync(null);
        await viewModel.ConfirmBookSeriesCommand.ExecuteAsync(null);

        Assert.Single(context.TimeEntryService.CreatedRequests);
        Assert.Equal(42, context.TimeEntryService.CreatedRequests[0].IssueId);
        Assert.Equal(2, context.TimeEntryService.CreatedRequests[0].Hours);
        Assert.Equal(RedmineDates.FormatSpentOn(monday), context.TimeEntryService.CreatedRequests[0].SpentOn);
        Assert.Contains("1 Zeiteinträge wurden erstellt", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SelectedTicket_change_ignores_stale_activity_load()
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

        context.TimeEntryService.ActivitiesDelay = TimeSpan.FromMilliseconds(150);
        context.TimeEntryService.ActivityFactory = issueId =>
        [
            new TimeEntryActivityDto { Id = issueId ?? 0, Name = $"Activity-{issueId}" }
        ];

        var viewModel = new SeriesBookingViewModel(context.SettingsService, context.TimeEntryService);
        viewModel.PrepareView();
        viewModel.SelectedTicket = viewModel.Tickets.First(ticket => ticket.Id == 100);
        viewModel.SelectedTicket = viewModel.Tickets.First(ticket => ticket.Id == 200);

        await Task.Delay(300);

        Assert.Equal(200, viewModel.SelectedActivity?.Id);
        Assert.Equal("Activity-200", viewModel.SelectedActivity?.Name);
    }

    private static DateTime GetPreviousMonday(DateTime date)
    {
        var offset = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-offset).Date;
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

        public List<TimeEntryCreateRequest> CreatedRequests { get; } = [];

        public TimeSpan ActivitiesDelay { get; set; }

        public Func<int?, IReadOnlyList<TimeEntryActivityDto>> ActivityFactory { get; set; } = _ => [];

        public async Task<IReadOnlyList<TimeEntryActivityDto>> GetActivitiesAsync(
            string baseUrl,
            string apiKey,
            int? issueId = null,
            int? projectId = null,
            CancellationToken cancellationToken = default)
        {
            if (ActivitiesDelay > TimeSpan.Zero)
            {
                await Task.Delay(ActivitiesDelay, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return ActivityFactory(issueId);
        }

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
            string? activityName = null,
            IReadOnlyList<TimeEntryCustomFieldValueDto>? existingValues = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(TimeEntryCustomFieldSupport.CreateRows([], [], settings, projectId, activityId, existingValues));

        public Task ResolveCustomFieldIdsAsync(
            AppSettings settings,
            ICollection<TimeEntryCustomFieldRowViewModel> rows,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<string>> TryAddMissingCustomFieldsFromBookingErrorAsync(
            AppSettings settings,
            ICollection<TimeEntryCustomFieldRowViewModel> rows,
            string bookingErrorMessage,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<TimeEntryLoadResult> GetMyTimeEntriesAsync(
            string baseUrl,
            string apiKey,
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new TimeEntryLoadResult { Success = true, Entries = Entries });

        public Task<TimeEntryOperationResult> CreateTimeEntryAsync(
            string baseUrl,
            string apiKey,
            TimeEntryCreateRequest request,
            CancellationToken cancellationToken = default)
        {
            CreatedRequests.Add(request);
            return Task.FromResult(new TimeEntryOperationResult { Success = true });
        }

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
