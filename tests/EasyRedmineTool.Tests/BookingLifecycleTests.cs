namespace EasyRedmineTool.Tests;

using EasyRedmineTool.Core.Configuration;
using EasyRedmineTool.Core.Models.Tickets;
using EasyRedmineTool.Core.Models.TimeEntries;
using EasyRedmineTool.Core.Services.Interfaces;
using EasyRedmineTool.Core.ViewModels;

public class BookingLifecycleTests
{
    [Fact]
    public async Task Reload_during_post_preserves_row_and_prevents_duplicate_request()
    {
        var settings = new MemorySettings();
        var service = new ControlledTimeService { PendingCreate = new() };
        using var vm = new TimeEntriesViewModel(settings, service);
        vm.ReloadFavorites();
        var row = Assert.Single(vm.FavoriteRows);
        row.SelectedActivity = new() { Id = 1 };
        row.Comments = "draft";
        var first = row.CreateTimeEntryCommand.ExecuteAsync(null);
        vm.ReloadFavorites();
        var visible = Assert.Single(vm.FavoriteRows);
        var second = visible.CreateTimeEntryCommand.ExecuteAsync(null);
        var wasSubmitting = visible.IsSubmitting;
        service.PendingCreate.SetResult(new() { Success = true });
        await Task.WhenAll(first, second);
        Assert.Same(row, visible);
        Assert.True(wasSubmitting);
        Assert.Single(service.Created);
    }

    [Fact]
    public void Reload_preserves_custom_field_draft_and_detail_state()
    {
        var settings = new MemorySettings();
        using var vm = new TimeEntriesViewModel(settings, new ControlledTimeService());
        vm.ReloadFavorites();
        var row = Assert.Single(vm.FavoriteRows);
        row.CustomFields.Add(new(1, "Field", false, false, [], "draft value"));
        row.AreDetailsExpanded = true;
        vm.ShowAllTicketsViewCommand.Execute(null);
        var again = Assert.Single(vm.FavoriteRows);
        Assert.Same(row, again);
        Assert.Equal("draft value", Assert.Single(again.CustomFields).Value);
        Assert.True(again.AreDetailsExpanded);
    }

    [Fact]
    public void Reload_500_tickets_does_not_eagerly_request_form_data()
    {
        var settings = new MemorySettings();
        settings.Value.CachedTickets = Enumerable.Range(1, 500).Select(id => new IssueDto { Id = id, Subject = "Task" }).ToList();
        var service = new ControlledTimeService();
        using var vm = new TimeEntriesViewModel(settings, service) { ShowFavoritesOnly = false };
        vm.ReloadFavorites();
        Assert.Equal(500, vm.FavoriteRows.Count);
        Assert.Equal(0, service.ActivityCalls);
        Assert.Equal(0, service.FieldCalls);
    }

    [Fact]
    public async Task Successful_post_stays_successful_when_local_defaults_write_fails()
    {
        var settings = new MemorySettings { FailWrites = true };
        var service = new ControlledTimeService();
        using var vm = new TimeEntriesViewModel(settings, service);
        vm.ReloadFavorites();
        var row = Assert.Single(vm.FavoriteRows);
        row.SelectedActivity = new() { Id = 1 };
        row.CustomFields.Add(new(1, "Field", false, false, [], "value"));
        row.Comments = "submitted";
        await row.CreateTimeEntryCommand.ExecuteAsync(null);
        Assert.Contains("gebucht", row.RowStatusMessage, StringComparison.Ordinal);
        Assert.Empty(row.Comments);
        Assert.Single(service.Created);
        Assert.True(service.EntryCalls > 0);
    }

    [Fact]
    public async Task Old_row_cannot_book_using_changed_connection_credentials()
    {
        var settings = new MemorySettings();
        var service = new ControlledTimeService();
        using var vm = new TimeEntriesViewModel(settings, service);
        vm.ReloadFavorites();
        var old = Assert.Single(vm.FavoriteRows);
        old.SelectedActivity = new() { Id = 1 };
        settings.Value.BaseUrl = "https://other.example/";
        settings.Value.ApiKey = "other-key";
        await old.CreateTimeEntryCommand.ExecuteAsync(null);
        Assert.Empty(service.Created);
    }

    [Fact]
    public async Task Today_response_from_previous_connection_is_not_published()
    {
        var settings = new MemorySettings();
        var service = new ControlledTimeService { PendingEntries = new() };
        using var vm = new TimeEntriesViewModel(settings, service);
        var task = vm.ReloadTodayBookedHoursAsync();
        settings.Value.BaseUrl = "https://other.example/";
        service.PendingEntries.SetResult(new() { Success = true, Entries = [new() { Id = 7, Hours = 3, Spent_On = EasyRedmineTool.Core.RedmineDates.TodayKey() }] });
        await task;
        Assert.Empty(vm.TodayTimeEntries);
        Assert.Equal(0, vm.TodayBookedHours);
    }

    [Fact]
    public async Task Visible_form_loads_are_bounded_and_completed_forms_are_reused()
    {
        var settings = new MemorySettings();
        settings.Value.CachedTickets = Enumerable.Range(1, 12).Select(id => new IssueDto { Id = id }).ToList();
        var service = new ControlledTimeService { PendingActivities = new() };
        using var vm = new TimeEntriesViewModel(settings, service) { ShowFavoritesOnly = false };
        vm.ReloadFavorites();
        var loads = vm.FavoriteRows.Select(row => row.EnsureFormLoadedAsync()).ToArray();
        Assert.Equal(4, service.ActivityCalls);
        service.PendingActivities.SetResult([new() { Id = 1 }]);
        await Task.WhenAll(loads);
        Assert.Equal(12, service.ActivityCalls);
        await vm.FavoriteRows[0].EnsureFormLoadedAsync();
        Assert.Equal(12, service.ActivityCalls);
    }

    [Fact]
    public async Task Edit_commit_is_not_reversed_when_defaults_cannot_be_saved()
    {
        var settings = new MemorySettings { FailWrites = true };
        var service = new ControlledTimeService();
        using var vm = new TimeEntriesViewModel(settings, service);
        var row = new TodayTimeEntryRowViewModel(vm, new() { Id = 5, Issue_Id = 42, Hours = 2 }, null, settings, service);
        row.BeginEditCommand.Execute(null);
        row.SelectedActivity = new() { Id = 1 };
        row.CustomFields.Add(new(1, "Field", false, false, [], "value"));
        await row.SaveEditCommand.ExecuteAsync(null);
        Assert.False(row.IsEditing);
        Assert.True(service.EntryCalls > 0);
    }

    [Fact]
    public async Task User_comment_typed_during_post_remains_as_next_draft()
    {
        var settings = new MemorySettings();
        var service = new ControlledTimeService { PendingCreate = new() };
        using var vm = new TimeEntriesViewModel(settings, service);
        vm.ReloadFavorites();
        var row = Assert.Single(vm.FavoriteRows);
        row.SelectedActivity = new() { Id = 1 };
        row.Comments = "submitted";
        var create = row.CreateTimeEntryCommand.ExecuteAsync(null);
        row.Comments = "next draft";
        service.PendingCreate.SetResult(new() { Success = true });
        await create;
        Assert.Equal("submitted", Assert.Single(service.Created).Comments);
        Assert.Equal("next draft", row.Comments);
    }

    [Fact]
    public async Task Reactivating_card_while_old_viewport_load_cancels_retries_form_load()
    {
        var settings = new MemorySettings();
        var service = new ControlledTimeService { PendingActivities = new() };
        using var vm = new TimeEntriesViewModel(settings, service);
        vm.ReloadFavorites();
        var row = Assert.Single(vm.FavoriteRows);
        using var viewport = new CancellationTokenSource();
        var first = row.EnsureFormLoadedAsync(viewport.Token);
        viewport.Cancel();
        var reactivated = row.EnsureFormLoadedAsync();
        service.PendingActivities.SetResult([new() { Id = 1 }]);
        await Task.WhenAll(first, reactivated);
        Assert.Single(row.Activities);
        Assert.NotNull(row.SelectedActivity);
        await row.EnsureFormLoadedAsync();
        Assert.Equal(2, service.ActivityCalls);
    }

    [Fact]
    public async Task Today_reload_preserves_edit_draft_and_row_identity()
    {
        var settings = new MemorySettings();
        var service = new ControlledTimeService { Entries = [new() { Id = 10, Issue_Id = 42, Hours = 1, Spent_On = EasyRedmineTool.Core.RedmineDates.TodayKey() }] };
        using var vm = new TimeEntriesViewModel(settings, service);
        await vm.ReloadTodayBookedHoursAsync();
        var row = Assert.Single(vm.TodayTimeEntries);
        row.BeginEditCommand.Execute(null);
        row.Comments = "unsaved draft";
        await vm.ReloadTodayBookedHoursAsync();
        Assert.Same(row, Assert.Single(vm.TodayTimeEntries));
        Assert.True(row.IsEditing);
        Assert.Equal("unsaved draft", row.Comments);
    }

    [Fact]
    public async Task Refresh_failure_after_edit_preserves_success_and_warns_without_removing_entry()
    {
        var settings = new MemorySettings();
        var service = new ControlledTimeService { Entries = [new() { Id = 10, Issue_Id = 42, Hours = 1, Spent_On = EasyRedmineTool.Core.RedmineDates.TodayKey() }] };
        using var vm = new TimeEntriesViewModel(settings, service);
        await vm.ReloadTodayBookedHoursAsync();
        var row = Assert.Single(vm.TodayTimeEntries);
        row.BeginEditCommand.Execute(null);
        row.SelectedActivity = new() { Id = 1 };
        service.FailEntries = true;
        await row.SaveEditCommand.ExecuteAsync(null);
        Assert.Same(row, Assert.Single(vm.TodayTimeEntries));
        Assert.False(row.IsEditing);
        Assert.Contains("commit succeeded", vm.StatusMessage, StringComparison.Ordinal);
        Assert.True(row.HasWarningMessage);
    }

    [Fact]
    public async Task Today_edit_reopen_preserves_the_committed_custom_field_snapshot()
    {
        var settings = new MemorySettings();
        var service = new ControlledTimeService
        {
            Entries = [new() { Id = 10, Issue_Id = 42, Hours = 1, Spent_On = EasyRedmineTool.Core.RedmineDates.TodayKey(),
                Custom_Fields = [new() { Id = 17, Name = "Field", Value = "old" }] }]
        };
        using var vm = new TimeEntriesViewModel(settings, service);
        await vm.ReloadTodayBookedHoursAsync();
        var row = Assert.Single(vm.TodayTimeEntries);
        row.BeginEditCommand.Execute(null);
        row.SelectedActivity = new() { Id = 1 };
        Assert.Single(row.CustomFields).Value = "new";
        await row.SaveEditCommand.ExecuteAsync(null);
        row.BeginEditCommand.Execute(null);
        Assert.Equal("new", Assert.Single(row.CustomFields).Value);
    }

    [Fact]
    public async Task Today_pending_update_retains_busy_identity_and_sends_only_once()
    {
        var settings = new MemorySettings();
        var service = new ControlledTimeService
        {
            Entries = [new() { Id = 10, Issue_Id = 42, Hours = 1, Spent_On = EasyRedmineTool.Core.RedmineDates.TodayKey() }],
            PendingUpdate = new()
        };
        using var vm = new TimeEntriesViewModel(settings, service);
        await vm.ReloadTodayBookedHoursAsync();
        var row = Assert.Single(vm.TodayTimeEntries);
        row.BeginEditCommand.Execute(null);
        row.SelectedActivity = new() { Id = 1 };
        var update = row.SaveEditCommand.ExecuteAsync(null);
        await vm.ReloadTodayBookedHoursAsync();
        var visible = Assert.Single(vm.TodayTimeEntries);
        Assert.Same(row, visible);
        Assert.True(visible.IsBusy);
        await visible.SaveEditCommand.ExecuteAsync(null);
        Assert.Equal(1, service.UpdateCalls);
        service.PendingUpdate.SetResult(new() { Success = true });
        await update;
    }

    [Fact]
    public async Task Today_delete_success_survives_failed_refresh_and_does_not_send_again()
    {
        var settings = new MemorySettings();
        var service = new ControlledTimeService
        {
            Entries = [new() { Id = 10, Issue_Id = 42, Hours = 1, Spent_On = EasyRedmineTool.Core.RedmineDates.TodayKey() }]
        };
        using var vm = new TimeEntriesViewModel(settings, service);
        await vm.ReloadTodayBookedHoursAsync();
        var row = Assert.Single(vm.TodayTimeEntries);
        service.FailEntries = true;
        await row.ConfirmDeleteCommand.ExecuteAsync(null);
        Assert.Empty(vm.TodayTimeEntries);
        Assert.True(vm.HasWarningMessage);
        Assert.Contains("delete committed", vm.StatusMessage, StringComparison.Ordinal);
        await row.ConfirmDeleteCommand.ExecuteAsync(null);
        Assert.Equal(1, service.DeleteCalls);
    }

    [Fact]
    public async Task Today_defaults_failure_remains_visible_on_reconciled_row()
    {
        var settings = new MemorySettings { FailWrites = true };
        var service = new ControlledTimeService
        {
            Entries = [new() { Id = 10, Issue_Id = 42, Hours = 1, Spent_On = EasyRedmineTool.Core.RedmineDates.TodayKey() }]
        };
        using var vm = new TimeEntriesViewModel(settings, service);
        await vm.ReloadTodayBookedHoursAsync();
        var row = Assert.Single(vm.TodayTimeEntries);
        row.BeginEditCommand.Execute(null);
        row.SelectedActivity = new() { Id = 1 };
        row.CustomFields.Add(new(17, "Field", false, false, [], "value"));
        await row.SaveEditCommand.ExecuteAsync(null);
        Assert.Same(row, Assert.Single(vm.TodayTimeEntries));
        Assert.True(row.HasWarningMessage);
        Assert.Contains("commit succeeded", vm.StatusMessage, StringComparison.Ordinal);
    }

    internal sealed class MemorySettings : IAppSettingsService
    {
        public AppSettings Value { get; set; } = new()
        {
            BaseUrl = "https://redmine.example/", ApiKey = "key", FavoriteTicketIds = [42],
            CachedTickets = [new IssueDto { Id = 42, Subject = "Task" }]
        };
        public bool FailWrites { get; set; }
        public string SettingsFilePath => "unused";
        public AppSettings Load() => Value;
        public void Save(AppSettings settings) => Value = settings;
        public void Update(Action<AppSettings> configure)
        {
            if (FailWrites) throw new IOException("local write failed");
            configure(Value);
        }
    }

    internal sealed class ControlledTimeService : ITimeEntryService
    {
        public TaskCompletionSource<TimeEntryOperationResult>? PendingCreate { get; set; }
        public TaskCompletionSource<TimeEntryOperationResult>? PendingUpdate { get; set; }
        public int UpdateCalls { get; private set; }
        public TaskCompletionSource<TimeEntryLoadResult>? PendingEntries { get; set; }
        public List<TimeEntryCreateRequest> Created { get; } = [];
        public TaskCompletionSource<IReadOnlyList<TimeEntryActivityDto>>? PendingActivities { get; set; }
        public int ActivityCalls { get; private set; }
        public int FieldCalls { get; private set; }
        public IReadOnlyList<TimeEntryDto> Entries { get; set; } = [];
        public bool FailEntries { get; set; }
        public int EntryCalls { get; private set; }
        public int DeleteCalls { get; private set; }
        public Task<IReadOnlyList<TimeEntryActivityDto>> GetActivitiesAsync(string baseUrl, string apiKey, int? issueId = null, int? projectId = null, CancellationToken cancellationToken = default)
        {
            ActivityCalls++;
            return PendingActivities?.Task ?? Task.FromResult<IReadOnlyList<TimeEntryActivityDto>>([new() { Id = 1, Name = "Dev" }]);
        }
        public Task<IReadOnlyList<TimeEntryCustomFieldValueDto>> GetRecentCustomFieldValuesAsync(string baseUrl, string apiKey, int? issueId = null, int? projectId = null, CancellationToken cancellationToken = default)
        {
            FieldCalls++;
            return Task.FromResult<IReadOnlyList<TimeEntryCustomFieldValueDto>>([]);
        }
        public Task<TimeEntryLoadResult> GetMyTimeEntriesAsync(string baseUrl, string apiKey, DateTime from, DateTime to, CancellationToken cancellationToken = default)
        {
            EntryCalls++;
            return PendingEntries?.Task ?? Task.FromResult(new TimeEntryLoadResult { Success = !FailEntries, Entries = Entries, Message = "refresh failed" });
        }
        public Task<TimeEntryOperationResult> CreateTimeEntryAsync(string baseUrl, string apiKey, TimeEntryCreateRequest request, CancellationToken cancellationToken = default)
        {
            Created.Add(request);
            return PendingCreate?.Task ?? Task.FromResult(new TimeEntryOperationResult { Success = true });
        }
        public Task<TimeEntryOperationResult> UpdateTimeEntryAsync(string baseUrl, string apiKey, int timeEntryId, TimeEntryUpdateRequest request, CancellationToken cancellationToken = default)
        {
            UpdateCalls++;
            return PendingUpdate?.Task ?? Task.FromResult(new TimeEntryOperationResult { Success = true, Message = "commit succeeded" });
        }
        public Task<TimeEntryOperationResult> DeleteTimeEntryAsync(string baseUrl, string apiKey, int timeEntryId, CancellationToken cancellationToken = default)
        {
            DeleteCalls++;
            return Task.FromResult(new TimeEntryOperationResult { Success = true, Message = "delete committed" });
        }
    }
}
