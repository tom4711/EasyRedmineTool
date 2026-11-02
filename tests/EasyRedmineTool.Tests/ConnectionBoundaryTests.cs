namespace EasyRedmineTool.Tests;

using EasyRedmineTool.Core;
using EasyRedmineTool.Core.Models.Tickets;
using EasyRedmineTool.Core.Models.TimeEntries;
using EasyRedmineTool.Core.Services.Interfaces;
using EasyRedmineTool.Core.ViewModels;

public class ConnectionBoundaryTests
{
    [Fact]
    public async Task Ticket_response_cannot_persist_into_a_changed_connection()
    {
        var settings = new BookingLifecycleTests.MemorySettings();
        var service = new PendingTickets();
        using var vm = new TicketListViewModel(service, settings);
        var load = vm.LoadTicketsCommand.ExecuteAsync(null);
        settings.Value.BaseUrl = "https://other.example/";
        settings.Value.CachedTickets.Clear();
        service.Result.SetResult(new() { Tickets = [new() { Id = 99, Subject = "old server" }] });
        await load;
        Assert.Empty(vm.Tickets);
        Assert.Empty(settings.Value.CachedTickets);
    }

    [Fact]
    public async Task Reload_connection_clears_busy_state_even_when_old_service_ignores_cancellation()
    {
        var settings = new BookingLifecycleTests.MemorySettings();
        var service = new PendingTickets();
        using var vm = new TicketListViewModel(service, settings);
        var load = vm.LoadTicketsCommand.ExecuteAsync(null);
        settings.Value.BaseUrl = "https://other.example/";
        settings.Value.CachedTickets.Clear();
        vm.ReloadSettings();
        Assert.False(vm.IsBusy);
        service.Result.SetResult(new() { Tickets = [new() { Id = 99 }] });
        await load;
        Assert.Empty(vm.Tickets);
    }

    [Fact]
    public async Task Weekly_response_cannot_publish_after_connection_change()
    {
        var settings = new BookingLifecycleTests.MemorySettings();
        var service = new BookingLifecycleTests.ControlledTimeService { PendingEntries = new() };
        using var vm = new WeeklySummaryViewModel(settings, service);
        var load = vm.ReloadWeeklySummaryAsync();
        settings.Value.ApiKey = "different-account";
        service.PendingEntries.SetResult(new() { Success = true, Entries = [new() { Id = 1, Hours = 7, Spent_On = RedmineDates.TodayKey() }] });
        await load;
        Assert.False(vm.HasLoadedData);
        Assert.Empty(vm.WeeklyHours);
    }

    [Fact]
    public async Task Committed_old_post_does_not_update_new_connection_cache_or_overview()
    {
        var settings = new BookingLifecycleTests.MemorySettings();
        var service = new BookingLifecycleTests.ControlledTimeService { PendingCreate = new() };
        using var vm = new TimeEntriesViewModel(settings, service);
        vm.ReloadFavorites();
        var old = Assert.Single(vm.FavoriteRows);
        old.SelectedActivity = new() { Id = 1 };
        var create = old.CreateTimeEntryCommand.ExecuteAsync(null);
        settings.Value.ApiKey = "different-account";
        settings.Value.CachedTickets = [new() { Id = 42, Subject = "new account" }];
        vm.ReloadFavorites();
        service.PendingCreate.SetResult(new() { Success = true });
        await create;
        Assert.Null(Assert.Single(settings.Value.CachedTickets).LastTimeEntryOn);
        Assert.Equal(0, service.EntryCalls);
        Assert.NotSame(old, Assert.Single(vm.FavoriteRows));
    }

    [Fact]
    public async Task Detached_today_entry_cannot_delete_against_new_connection()
    {
        var settings = new BookingLifecycleTests.MemorySettings();
        var service = new BookingLifecycleTests.ControlledTimeService();
        using var vm = new TimeEntriesViewModel(settings, service);
        var row = new TodayTimeEntryRowViewModel(vm, new() { Id = 99 }, null, settings, service);
        settings.Value.BaseUrl = "https://other.example/";
        await row.ConfirmDeleteCommand.ExecuteAsync(null);
        Assert.Equal(0, service.DeleteCalls);
    }

    [Fact]
    public async Task Old_post_cannot_publish_after_switch_away_and_back()
    {
        var settings = new BookingLifecycleTests.MemorySettings();
        var service = new BookingLifecycleTests.ControlledTimeService { PendingCreate = new() };
        using var vm = new TimeEntriesViewModel(settings, service);
        vm.ReloadFavorites();
        var old = Assert.Single(vm.FavoriteRows);
        old.SelectedActivity = new() { Id = 1 };
        var create = old.CreateTimeEntryCommand.ExecuteAsync(null);
        settings.Value.ApiKey = "other";
        vm.ReloadFavorites();
        settings.Value.ApiKey = "key";
        settings.Value.CachedTickets = [new() { Id = 42 }];
        vm.ReloadFavorites();
        service.PendingCreate.SetResult(new() { Success = true });
        await create;
        Assert.Null(Assert.Single(settings.Value.CachedTickets).LastTimeEntryOn);
        Assert.Equal(0, service.EntryCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Old_status_failure_cannot_replace_new_connection_filters(bool apiError)
    {
        var settings = new BookingLifecycleTests.MemorySettings();
        var service = new PendingTickets();
        using var vm = new TicketListViewModel(service, settings);
        service.PendingStatus = new();
        var old = vm.ReloadStatusFilterOptionsAsync();
        settings.Value.BaseUrl = "https://other.example/";
        vm.ReloadSettings();
        Assert.Contains(vm.StatusFilterOptions, option => option.Value.StatusId == 7);
        service.PendingStatus.SetException(apiError
            ? new EasyRedmineTool.Core.Api.RedmineApiException("statuses", 500, "old failure", "")
            : new InvalidOperationException("old failure"));
        await old;
        Assert.Contains(vm.StatusFilterOptions, option => option.Value.StatusId == 7);
        Assert.DoesNotContain("old failure", vm.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Direct_today_reload_after_connection_change_creates_current_rows()
    {
        var settings = new BookingLifecycleTests.MemorySettings();
        var service = new BookingLifecycleTests.ControlledTimeService
        {
            Entries = [new() { Id = 10, Issue_Id = 42, Hours = 1, Spent_On = RedmineDates.TodayKey() }]
        };
        using var vm = new TimeEntriesViewModel(settings, service);
        await vm.ReloadTodayBookedHoursAsync();
        var original = Assert.Single(vm.TodayTimeEntries);
        settings.Value.ApiKey = "other";
        await vm.ReloadTodayBookedHoursAsync();
        Assert.NotSame(original, Assert.Single(vm.TodayTimeEntries));
    }

    private sealed class PendingTickets : ITicketService
    {
        public TaskCompletionSource<TicketListLoadResult> Result { get; } = new();
        public TaskCompletionSource<IReadOnlyList<StatusDto>>? PendingStatus { get; set; }
        public Task<IReadOnlyList<IssueDto>> GetMyOpenIssuesAsync(string baseUrl, string apiKey, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<IssueDto>>([]);
        public Task<TicketListLoadResult> GetTicketsForListAsync(string baseUrl, string apiKey, TicketLoadFilter filter, CancellationToken cancellationToken = default) => Result.Task;
        public Task<IssueDto?> GetIssueByIdAsync(string baseUrl, string apiKey, int issueId, CancellationToken cancellationToken = default) => Task.FromResult<IssueDto?>(null);
        public Task<IReadOnlyList<StatusDto>> GetIssueStatusesAsync(string baseUrl, string apiKey, CancellationToken cancellationToken = default) =>
            baseUrl.Contains("other", StringComparison.Ordinal) ? Task.FromResult<IReadOnlyList<StatusDto>>([new() { Id = 7, Name = "B status" }]) : PendingStatus?.Task ?? Task.FromResult<IReadOnlyList<StatusDto>>([]);
        public void InvalidateTimeEntryCache() { }
    }
}
