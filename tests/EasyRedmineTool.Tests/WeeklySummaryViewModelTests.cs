namespace EasyRedmineTool.Tests;

using EasyRedmineTool.Core;
using EasyRedmineTool.Core.Configuration;
using EasyRedmineTool.Core.Models.TimeEntries;
using EasyRedmineTool.Core.Models.Tickets;
using EasyRedmineTool.Core.Services;
using EasyRedmineTool.Core.Services.Interfaces;
using EasyRedmineTool.Core.ViewModels;

using Microsoft.Extensions.Logging.Abstractions;

public class WeeklySummaryViewModelTests
{
    [Fact]
    public void PrepareView_does_not_request_time_entries()
    {
        using var context = TestContext.Create();
        context.SettingsService.Save(new AppSettings
        {
            BaseUrl = "https://redmine.example/",
            ApiKey = "secret"
        });

        var viewModel = new WeeklySummaryViewModel(context.SettingsService, context.TimeEntryService);
        viewModel.PrepareView();

        Assert.Equal(0, context.TimeEntryService.LoadCallCount);
        Assert.False(viewModel.HasLoadedData);
        Assert.Contains("Daten laden", viewModel.StatusMessage, StringComparison.Ordinal);
        Assert.Contains("Q", viewModel.CurrentQuarterLabel, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReloadWeeklySummaryAsync_shows_message_when_api_key_missing()
    {
        using var context = TestContext.Create();
        var viewModel = new WeeklySummaryViewModel(context.SettingsService, context.TimeEntryService);

        await viewModel.ReloadWeeklySummaryAsync();

        Assert.Contains("API-Key fehlt", viewModel.StatusMessage, StringComparison.Ordinal);
        Assert.Empty(viewModel.WeeklyHours);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task ReloadWeeklySummaryAsync_uses_latest_request_when_called_twice()
    {
        using var context = TestContext.Create();
        context.SettingsService.Save(new AppSettings
        {
            BaseUrl = "https://redmine.example/",
            ApiKey = "secret"
        });
        context.TimeEntryService.FirstCallDelay = TimeSpan.FromMilliseconds(150);
        context.TimeEntryService.NextEntries =
        [
            new TimeEntryDto
            {
                Issue_Id = 1,
                Spent_On = RedmineDates.FormatSpentOn(DateTime.Today),
                Hours = 99
            }
        ];
        context.TimeEntryService.LatestEntries =
        [
            new TimeEntryDto
            {
                Issue_Id = 2,
                Spent_On = RedmineDates.FormatSpentOn(DateTime.Today),
                Hours = 2
            }
        ];

        var viewModel = new WeeklySummaryViewModel(context.SettingsService, context.TimeEntryService);

        var firstCall = viewModel.ReloadWeeklySummaryAsync();
        await viewModel.ReloadWeeklySummaryAsync();
        await firstCall;

        Assert.Equal(2, viewModel.WeeklyTotalHours);
        Assert.True(viewModel.HasLoadedData);
        Assert.Equal("Aktualisieren", viewModel.LoadActionLabel);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task PrepareView_keeps_loaded_data_without_new_request()
    {
        using var context = TestContext.Create();
        context.SettingsService.Save(new AppSettings
        {
            BaseUrl = "https://redmine.example/",
            ApiKey = "secret"
        });
        context.TimeEntryService.NextEntries =
        [
            new TimeEntryDto
            {
                Issue_Id = 1,
                Spent_On = RedmineDates.FormatSpentOn(DateTime.Today),
                Hours = 4
            }
        ];

        var viewModel = new WeeklySummaryViewModel(context.SettingsService, context.TimeEntryService);
        await viewModel.ReloadWeeklySummaryAsync();
        var callsAfterLoad = context.TimeEntryService.LoadCallCount;

        viewModel.PrepareView();

        Assert.Equal(callsAfterLoad, context.TimeEntryService.LoadCallCount);
        Assert.True(viewModel.HasLoadedData);
        Assert.Equal(4, viewModel.WeeklyTotalHours);
    }

    private sealed class TestContext : IDisposable
    {
        private readonly string _settingsPath;

        private TestContext(string settingsPath, AppSettingsService settingsService, DelayedTimeEntryService timeEntryService)
        {
            _settingsPath = settingsPath;
            SettingsService = settingsService;
            TimeEntryService = timeEntryService;
        }

        public AppSettingsService SettingsService { get; }
        public DelayedTimeEntryService TimeEntryService { get; }

        public static TestContext Create()
        {
            var settingsPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
            var settingsService = new AppSettingsService(settingsPath, NullLogger<AppSettingsService>.Instance);
            return new TestContext(settingsPath, settingsService, new DelayedTimeEntryService());
        }

        public void Dispose()
        {
            if (File.Exists(_settingsPath))
            {
                File.Delete(_settingsPath);
            }
        }
    }

    private sealed class DelayedTimeEntryService : ITimeEntryService
    {
        private int _callCount;

        public int LoadCallCount => _callCount;

        public TimeSpan FirstCallDelay { get; set; }
        public IReadOnlyList<TimeEntryDto> NextEntries { get; set; } = [];
        public IReadOnlyList<TimeEntryDto> LatestEntries { get; set; } = [];

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

        public async Task<TimeEntryLoadResult> GetMyTimeEntriesAsync(
            string baseUrl,
            string apiKey,
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken = default)
        {
            _callCount++;
            if (_callCount == 1 && FirstCallDelay > TimeSpan.Zero)
            {
                await Task.Delay(FirstCallDelay, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();

            var entries = _callCount == 1 ? NextEntries : LatestEntries;
            return new TimeEntryLoadResult
            {
                Success = true,
                Entries = entries
            };
        }

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
