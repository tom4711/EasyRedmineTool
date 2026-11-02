namespace EasyRedmineTool.Tests;

using EasyRedmineTool.Core;
using EasyRedmineTool.Core.Configuration;
using EasyRedmineTool.Core.Models.TimeEntries;
using EasyRedmineTool.Core.Models.Tickets;
using EasyRedmineTool.Core.Services.Interfaces;
using EasyRedmineTool.Core.ViewModels;

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
        viewModel.RequestBookSeriesCommand.Execute(null);
        Assert.True(viewModel.ShowBookingConfirmation);
        Assert.Equal("1 Eintrag buchen", viewModel.BookingConfirmationActionLabel);
        Assert.Contains("1 Einträge × 2 h = 2 h", viewModel.BookingConfirmationText, StringComparison.Ordinal);
        Assert.Contains("Ticket #42: Task", viewModel.BookingConfirmationText, StringComparison.Ordinal);
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

    [Fact]
    public async Task RefreshPreview_discards_response_after_inputs_change()
    {
        using var context = TestContext.Create();
        context.SettingsService.Save(new AppSettings
        {
            ApiKey = "secret", FavoriteTicketIds = [42],
            CachedTickets = [new IssueDto { Id = 42, Subject = "Task" }]
        });
        var pending = new TaskCompletionSource<TimeEntryLoadResult>();
        context.TimeEntryService.PendingPreview = pending;
        using var viewModel = new SeriesBookingViewModel(context.SettingsService, context.TimeEntryService);
        viewModel.PrepareView();
        viewModel.FromDate = GetPreviousMonday(DateTime.Today);
        viewModel.ToDate = viewModel.FromDate;
        viewModel.SelectedActivity = new TimeEntryActivityDto { Id = 1 };
        var request = viewModel.RefreshPreviewCommand.ExecuteAsync(null);
        viewModel.FromDate = viewModel.FromDate.Value.AddDays(-7);
        pending.SetResult(new TimeEntryLoadResult { Success = true, Entries = [] });
        await request;
        Assert.False(viewModel.HasPreview);
        Assert.Empty(viewModel.PreviewRows);
        Assert.False(viewModel.CanBook);
    }

    [Fact]
    public async Task Preview_notifies_CanBook_true_after_loading_finishes()
    {
        using var context = TestContext.Create();
        using var viewModel = CreateReadyViewModel(context);
        var observed = ObserveCanBook(viewModel);

        await viewModel.RefreshPreviewCommand.ExecuteAsync(null);

        Assert.True(viewModel.CanBook);
        Assert.True(observed[^1]);
        Assert.Contains(false, observed);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Preview_refresh_notifies_busy_start_and_end_even_on_failure(bool succeeds)
    {
        using var context = TestContext.Create();
        using var viewModel = CreateReadyViewModel(context);
        await viewModel.RefreshPreviewCommand.ExecuteAsync(null);
        var observed = ObserveCanBook(viewModel);
        var pending = new TaskCompletionSource<TimeEntryLoadResult>();
        context.TimeEntryService.PendingPreview = pending;

        var refresh = viewModel.RefreshPreviewCommand.ExecuteAsync(null);
        Assert.False(viewModel.CanBook);
        Assert.NotEmpty(observed);
        Assert.False(observed[^1]);
        pending.SetResult(new TimeEntryLoadResult { Success = succeeds, Entries = [], Message = "Offline" });
        await refresh;

        Assert.True(viewModel.CanBook);
        Assert.True(observed[^1]);
    }

    [Fact]
    public async Task Booking_failure_notifies_CanBook_true_after_booking_finishes()
    {
        using var context = TestContext.Create();
        using var viewModel = CreateReadyViewModel(context);
        await viewModel.RefreshPreviewCommand.ExecuteAsync(null);
        var observed = ObserveCanBook(viewModel);
        var pending = new TaskCompletionSource<TimeEntryOperationResult>();
        context.TimeEntryService.CreateResult = _ => pending.Task;
        viewModel.RequestBookSeriesCommand.Execute(null);

        var booking = viewModel.ConfirmBookSeriesCommand.ExecuteAsync(null);
        Assert.False(viewModel.CanBook);
        Assert.NotEmpty(observed);
        Assert.False(observed[^1]);
        pending.SetResult(new TimeEntryOperationResult { Success = false, Message = "Denied" });
        await booking;

        Assert.True(viewModel.CanBook);
        Assert.True(observed[^1]);
        Assert.Single(context.TimeEntryService.CreatedRequests);
    }

    [Fact]
    public async Task Successful_booking_refreshes_server_and_warns_when_reselecting_booked_day()
    {
        using var context = TestContext.Create();
        using var viewModel = CreateReadyViewModel(context);
        await viewModel.RefreshPreviewCommand.ExecuteAsync(null);
        viewModel.RequestBookSeriesCommand.Execute(null);
        await viewModel.ConfirmBookSeriesCommand.ExecuteAsync(null);

        Assert.Equal(2, context.TimeEntryService.PreviewReads);
        var row = Assert.Single(viewModel.PreviewRows);
        Assert.True(row.HasConflict);
        Assert.False(row.IsSelected);
        row.IsSelected = true;
        viewModel.RequestBookSeriesCommand.Execute(null);
        Assert.Contains("1 Konflikttage", viewModel.BookingConfirmationText, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Partial_booking_marks_success_immediately_and_retains_only_failed_day_selected(bool refreshSucceeds)
    {
        using var context = TestContext.Create();
        using var viewModel = CreateReadyViewModel(context, days: 2);
        await viewModel.RefreshPreviewCommand.ExecuteAsync(null);
        var secondResult = new TaskCompletionSource<TimeEntryOperationResult>();
        context.TimeEntryService.CreateResult = _ => context.TimeEntryService.CreatedRequests.Count == 1
            ? Task.FromResult(new TimeEntryOperationResult { Success = true })
            : secondResult.Task;
        viewModel.RequestBookSeriesCommand.Execute(null);

        var booking = viewModel.ConfirmBookSeriesCommand.ExecuteAsync(null);
        var firstRow = viewModel.PreviewRows[0];
        var markedImmediately = firstRow.HasConflict && !firstRow.IsSelected;
        context.TimeEntryService.PreviewSucceeds = refreshSucceeds;
        secondResult.SetResult(new TimeEntryOperationResult { Success = false, Message = "Denied" });
        await booking;

        Assert.True(markedImmediately);
        Assert.Equal(2, context.TimeEntryService.PreviewReads);
        Assert.True(viewModel.PreviewRows[0].HasConflict);
        Assert.False(viewModel.PreviewRows[0].IsSelected);
        Assert.False(viewModel.PreviewRows[1].HasConflict);
        Assert.True(viewModel.PreviewRows[1].IsSelected);
        Assert.Contains("1 erstellt, 1 fehlgeschlagen", viewModel.StatusMessage, StringComparison.Ordinal);
        if (refreshSucceeds)
        {
            Assert.Empty(viewModel.WarningMessage);
        }
        else
        {
            Assert.Contains("Vorschau", viewModel.WarningMessage, StringComparison.Ordinal);
        }
        viewModel.RequestBookSeriesCommand.Execute(null);
        Assert.Equal("1 Eintrag buchen", viewModel.BookingConfirmationActionLabel);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Successful_post_stays_successful_when_local_defaults_fail(bool failOnFlush)
    {
        using var context = TestContext.Create();
        using var viewModel = CreateReadyViewModel(context);
        viewModel.CustomFields.Add(new TimeEntryCustomFieldRowViewModel(7, "Team", true, false, [], "Core"));
        await viewModel.RefreshPreviewCommand.ExecuteAsync(null);
        context.SettingsService.FailUpdate = !failOnFlush;
        context.SettingsService.FailFlush = failOnFlush;
        viewModel.RequestBookSeriesCommand.Execute(null);

        var error = await Record.ExceptionAsync(() => viewModel.ConfirmBookSeriesCommand.ExecuteAsync(null));

        Assert.Null(error);
        Assert.Single(context.TimeEntryService.CreatedRequests);
        Assert.Contains("1 Zeiteinträge wurden erstellt", viewModel.StatusMessage, StringComparison.Ordinal);
        Assert.True(Assert.Single(viewModel.PreviewRows).HasConflict);
        Assert.False(viewModel.CanBook);
        Assert.Equal(2, context.TimeEntryService.PreviewReads);
        Assert.Equal(failOnFlush ? 1 : 0, context.SettingsService.FlushCount);
        Assert.Contains("lokale Vorgaben", viewModel.WarningMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Preview_from_previous_connection_cannot_publish_after_settings_change()
    {
        using var context = TestContext.Create();
        using var viewModel = CreateReadyViewModel(context);
        var pending = new TaskCompletionSource<TimeEntryLoadResult>();
        context.TimeEntryService.PendingPreview = pending;
        var preview = viewModel.RefreshPreviewCommand.ExecuteAsync(null);
        context.SettingsService.Save(new AppSettings { BaseUrl = "https://other.example", ApiKey = "other-key" });
        viewModel.StatusMessage = "Connection changed";

        pending.SetResult(new TimeEntryLoadResult { Success = true, Entries = [] });
        await preview;

        Assert.Empty(viewModel.PreviewRows);
        Assert.False(viewModel.HasPreview);
        Assert.Equal("Connection changed", viewModel.StatusMessage);
    }

    [Fact]
    public async Task Series_stops_after_inflight_post_when_connection_changes()
    {
        using var context = TestContext.Create();
        using var viewModel = CreateReadyViewModel(context, days: 2);
        await viewModel.RefreshPreviewCommand.ExecuteAsync(null);
        var pending = new TaskCompletionSource<TimeEntryOperationResult>();
        context.TimeEntryService.CreateResult = _ => pending.Task;
        viewModel.RequestBookSeriesCommand.Execute(null);
        var booking = viewModel.ConfirmBookSeriesCommand.ExecuteAsync(null);
        context.SettingsService.Save(new AppSettings { BaseUrl = "https://other.example", ApiKey = "other-key" });
        viewModel.PrepareView();
        viewModel.StatusMessage = "Connection changed";

        pending.SetResult(new TimeEntryOperationResult { Success = true });
        await booking;

        Assert.Single(context.TimeEntryService.CreatedRequests);
        Assert.Equal(0, context.SettingsService.FlushCount);
        Assert.Equal("Connection changed", viewModel.StatusMessage);
        Assert.Empty(viewModel.PreviewRows);
        Assert.False(viewModel.IsBooking);
    }

    [Fact]
    public async Task Details_from_previous_connection_cannot_publish_after_settings_change()
    {
        using var context = TestContext.Create();
        var pending = new TaskCompletionSource<IReadOnlyList<TimeEntryActivityDto>>();
        context.TimeEntryService.PendingActivities = pending;
        using var viewModel = CreateReadyViewModel(context);
        context.SettingsService.Save(new AppSettings { BaseUrl = "https://other.example", ApiKey = "other-key" });

        pending.SetResult([new TimeEntryActivityDto { Id = 99, Name = "Old connection" }]);
        await context.TimeEntryService.ActivityReturned.Task;
        // Flush the async detail continuation deterministically via its inline task completion.
        Assert.DoesNotContain(viewModel.Activities, activity => activity.Id == 99);
    }

    [Fact]
    public void Old_ticket_cannot_start_detail_requests_using_new_credentials()
    {
        using var context = TestContext.Create();
        using var viewModel = CreateReadyViewModel(context);
        var initialRequests = context.TimeEntryService.ActivityRequests;
        context.SettingsService.Save(new AppSettings { BaseUrl = "https://other.example", ApiKey = "other-key" });

        viewModel.SelectedTicket = new IssueDto { Id = 43, Subject = "Old cached ticket" };

        Assert.Equal(initialRequests, context.TimeEntryService.ActivityRequests);
    }

    [Fact]
    public async Task Reset_cancels_preview_and_clears_all_state_even_when_connection_returns_to_original()
    {
        using var context = TestContext.Create();
        using var viewModel = CreateReadyViewModel(context);
        await viewModel.RefreshPreviewCommand.ExecuteAsync(null);
        viewModel.RequestBookSeriesCommand.Execute(null);
        viewModel.WarningMessage = "Old warning";
        var originalSettings = context.SettingsService.Load();
        var pending = new TaskCompletionSource<TimeEntryLoadResult>();
        context.TimeEntryService.PendingPreview = pending;
        var preview = viewModel.RefreshPreviewCommand.ExecuteAsync(null);
        var token = context.TimeEntryService.LastPreviewToken;
        context.SettingsService.Save(new AppSettings { BaseUrl = "https://other.example", ApiKey = "other-key" });

        viewModel.ResetForConnectionChange();
        Assert.True(token.IsCancellationRequested);
        Assert.Empty(viewModel.Tickets);
        Assert.Null(viewModel.SelectedTicket);
        Assert.Empty(viewModel.Activities);
        Assert.Empty(viewModel.CustomFields);
        Assert.Empty(viewModel.PreviewRows);
        Assert.False(viewModel.ShowBookingConfirmation);
        Assert.False(viewModel.HasPreview);
        Assert.False(viewModel.HasWarningMessage);
        Assert.False(viewModel.IsLoadingPreview);
        context.SettingsService.Save(originalSettings);
        viewModel.ResetForConnectionChange();
        viewModel.StatusMessage = "New generation";
        pending.SetResult(new TimeEntryLoadResult { Success = true, Entries = [] });
        await preview;

        Assert.Empty(viewModel.PreviewRows);
        Assert.Equal("New generation", viewModel.StatusMessage);
    }

    [Theory]
    [InlineData(true, true, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    public async Task Booking_preserves_new_preview_during_delayed_defaults_flush(
        bool switchTicket, bool completeNewPreviewFirst, bool flushFails)
    {
        using var context = TestContext.Create();
        using var viewModel = CreateReadyViewModel(context);
        await viewModel.RefreshPreviewCommand.ExecuteAsync(null);
        var pendingFlush = new TaskCompletionSource();
        context.SettingsService.PendingFlush = pendingFlush;
        viewModel.RequestBookSeriesCommand.Execute(null);
        var booking = viewModel.ConfirmBookSeriesCommand.ExecuteAsync(null);
        Assert.Equal(1, context.SettingsService.FlushCount);
        Assert.False(viewModel.IsBooking);

        if (switchTicket)
        {
            viewModel.SelectedTicket = new IssueDto { Id = 43, Subject = "Other task" };
            viewModel.SelectedActivity = new TimeEntryActivityDto { Id = 3, Name = "Dev" };
        }
        viewModel.Comments = "New draft";
        var pendingPreview = new TaskCompletionSource<TimeEntryLoadResult>();
        context.TimeEntryService.PendingPreview = pendingPreview;
        var newPreview = viewModel.RefreshPreviewCommand.ExecuteAsync(null);
        if (completeNewPreviewFirst)
        {
            pendingPreview.SetResult(new TimeEntryLoadResult { Success = true, Entries = context.TimeEntryService.Entries });
            await newPreview;
        }
        var newStatus = viewModel.StatusMessage;
        var newRow = viewModel.PreviewRows.FirstOrDefault();
        var newIsLoading = viewModel.IsLoadingPreview;

        if (flushFails)
        {
            pendingFlush.SetException(new IOException("Disk unavailable"));
        }
        else
        {
            pendingFlush.SetResult();
        }
        await booking;
        var statusAfterBooking = viewModel.StatusMessage;
        var warningAfterBooking = viewModel.WarningMessage;
        var rowAfterBooking = viewModel.PreviewRows.FirstOrDefault();
        var isLoadingAfterBooking = viewModel.IsLoadingPreview;
        var readsAfterBooking = context.TimeEntryService.PreviewReads;
        var commentsAfterBooking = viewModel.Comments;
        if (!completeNewPreviewFirst)
        {
            pendingPreview.SetResult(new TimeEntryLoadResult { Success = true, Entries = context.TimeEntryService.Entries });
            await newPreview;
        }

        Assert.Single(context.TimeEntryService.CreatedRequests);
        Assert.Equal(42, context.TimeEntryService.CreatedRequests[0].IssueId);
        Assert.Equal(2, readsAfterBooking);
        Assert.Equal(newStatus, statusAfterBooking);
        Assert.Empty(warningAfterBooking);
        Assert.Same(newRow, rowAfterBooking);
        Assert.Equal(newIsLoading, isLoadingAfterBooking);
        Assert.Equal("New draft", commentsAfterBooking);
        if (switchTicket)
        {
            Assert.False(Assert.Single(viewModel.PreviewRows).HasConflict);
            Assert.True(viewModel.CanBook);
        }
    }

    [Fact]
    public async Task Booking_refresh_completion_cannot_override_new_ticket_preview_or_busy_state()
    {
        using var context = TestContext.Create();
        using var viewModel = CreateReadyViewModel(context);
        await viewModel.RefreshPreviewCommand.ExecuteAsync(null);
        var oldPreviewResult = new TaskCompletionSource<TimeEntryLoadResult>();
        context.TimeEntryService.PendingPreview = oldPreviewResult;
        viewModel.RequestBookSeriesCommand.Execute(null);
        var booking = viewModel.ConfirmBookSeriesCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsLoadingPreview);
        viewModel.SelectedTicket = new IssueDto { Id = 43, Subject = "Other task" };
        viewModel.SelectedActivity = new TimeEntryActivityDto { Id = 3, Name = "Dev" };
        viewModel.Comments = "New draft";
        var newPreviewResult = new TaskCompletionSource<TimeEntryLoadResult>();
        context.TimeEntryService.PendingPreview = newPreviewResult;
        var newPreview = viewModel.RefreshPreviewCommand.ExecuteAsync(null);
        var newStatus = viewModel.StatusMessage;

        oldPreviewResult.SetResult(new TimeEntryLoadResult { Success = true, Entries = context.TimeEntryService.Entries });
        await booking;
        var statusAfterBooking = viewModel.StatusMessage;
        var warningAfterBooking = viewModel.WarningMessage;
        var isLoadingAfterBooking = viewModel.IsLoadingPreview;
        newPreviewResult.SetResult(new TimeEntryLoadResult { Success = true, Entries = context.TimeEntryService.Entries });
        await newPreview;

        Assert.Equal(newStatus, statusAfterBooking);
        Assert.Empty(warningAfterBooking);
        Assert.True(isLoadingAfterBooking);
        Assert.False(Assert.Single(viewModel.PreviewRows).HasConflict);
        Assert.True(viewModel.CanBook);
        Assert.Equal("New draft", viewModel.Comments);
        Assert.Single(context.TimeEntryService.CreatedRequests);
    }

    private static SeriesBookingViewModel CreateReadyViewModel(TestContext context, int days = 1)
    {
        context.SettingsService.Save(new AppSettings
        {
            BaseUrl = "https://redmine.example/", ApiKey = "secret", FavoriteTicketIds = [42],
            CachedTickets = [new IssueDto { Id = 42, Subject = "Task" }]
        });
        var viewModel = new SeriesBookingViewModel(context.SettingsService, context.TimeEntryService);
        viewModel.PrepareView();
        viewModel.FromDate = new DateTime(2026, 10, 26);
        viewModel.ToDate = new DateTime(2026, 10, 26).AddDays(days - 1);
        viewModel.SelectedActivity = new TimeEntryActivityDto { Id = 3, Name = "Dev" };
        return viewModel;
    }

    private static List<bool> ObserveCanBook(SeriesBookingViewModel viewModel)
    {
        var values = new List<bool>();
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(viewModel.CanBook))
            {
                values.Add(viewModel.CanBook);
            }
        };
        return values;
    }

    private static DateTime GetPreviousMonday(DateTime date)
    {
        var offset = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-offset).Date;
    }

    private sealed class TestContext : IDisposable
    {
        public FakeSettingsService SettingsService { get; } = new();
        public FakeTimeEntryService TimeEntryService { get; } = new();
        public static TestContext Create() => new();
        public void Dispose() { }
    }

    private sealed class FakeSettingsService : IAppSettingsService
    {
        private AppSettings _settings = new();
        public string SettingsFilePath => string.Empty;
        public bool FailUpdate { get; set; }
        public bool FailFlush { get; set; }
        public int FlushCount { get; private set; }
        public TaskCompletionSource? PendingFlush { get; set; }
        public AppSettings Load() => _settings;
        public void Save(AppSettings settings) => _settings = settings;
        public void Update(Action<AppSettings> configure)
        {
            if (FailUpdate)
            {
                throw new IOException("Disk unavailable");
            }
            configure(_settings);
        }
        public Task FlushAsync(CancellationToken cancellationToken = default)
        {
            FlushCount++;
            return PendingFlush?.Task ?? (FailFlush ? Task.FromException(new IOException("Disk unavailable")) : Task.CompletedTask);
        }
    }

    private sealed class FakeTimeEntryService : ITimeEntryService
    {
        public IReadOnlyList<TimeEntryDto> Entries { get; set; } = [];

        public List<TimeEntryCreateRequest> CreatedRequests { get; } = [];

        public TaskCompletionSource<TimeEntryLoadResult>? PendingPreview { get; set; }

        public TaskCompletionSource<IReadOnlyList<TimeEntryActivityDto>>? PendingActivities { get; set; }
        public TaskCompletionSource ActivityReturned { get; } = new();

        public int ActivityRequests { get; private set; }
        public CancellationToken LastPreviewToken { get; private set; }
        public int PreviewReads { get; private set; }
        public bool PreviewSucceeds { get; set; } = true;
        public Func<TimeEntryCreateRequest, Task<TimeEntryOperationResult>>? CreateResult { get; set; }

        public TimeSpan ActivitiesDelay { get; set; }

        public Func<int?, IReadOnlyList<TimeEntryActivityDto>> ActivityFactory { get; set; } = _ => [];

        public async Task<IReadOnlyList<TimeEntryActivityDto>> GetActivitiesAsync(
            string baseUrl,
            string apiKey,
            int? issueId = null,
            int? projectId = null,
            CancellationToken cancellationToken = default)
        {
            ActivityRequests++;
            if (PendingActivities is not null)
            {
                var result = await PendingActivities.Task;
                ActivityReturned.TrySetResult();
                return result;
            }

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
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TimeEntryCustomFieldValueDto>>([]);

        public Task<TimeEntryLoadResult> GetMyTimeEntriesAsync(
            string baseUrl,
            string apiKey,
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken = default)
        {
            PreviewReads++;
            LastPreviewToken = cancellationToken;
            return PendingPreview?.Task ?? Task.FromResult(new TimeEntryLoadResult
            {
                Success = PreviewSucceeds, Entries = Entries, Message = PreviewSucceeds ? string.Empty : "Offline"
            });
        }

        public async Task<TimeEntryOperationResult> CreateTimeEntryAsync(
            string baseUrl,
            string apiKey,
            TimeEntryCreateRequest request,
            CancellationToken cancellationToken = default)
        {
            CreatedRequests.Add(request);
            var result = CreateResult is null
                ? new TimeEntryOperationResult { Success = true }
                : await CreateResult(request);
            if (result.Success)
            {
                Entries = [.. Entries, new TimeEntryDto
                {
                    Issue_Id = request.IssueId, Spent_On = request.SpentOn, Hours = request.Hours
                }];
            }
            return result;
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
