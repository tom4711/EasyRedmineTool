namespace EasyRedmineTool.Desktop.Tests;

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using EasyRedmineTool.Core.Configuration;
using EasyRedmineTool.Core.Models.TimeEntries;
using EasyRedmineTool.Core.Models.Tickets;
using EasyRedmineTool.Core.Services.Interfaces;
using EasyRedmineTool.Core.ViewModels;
using EasyRedmineTool.Desktop.Views;

public sealed class TimeEntriesViewVirtualizationTests
{
    [AvaloniaFact]
    public async Task Five_hundred_tickets_realize_viewport_only_and_recycling_retains_draft()
    {
        var settings = new MemorySettings(new AppSettings
        {
            BaseUrl = "https://fake.example/", ApiKey = "fake-key",
            CachedTickets = Enumerable.Range(1, 500).Select(id => new IssueDto { Id = id, Subject = $"Ticket {id}" }).ToList()
        });
        var service = new FormService();
        using var vm = new TimeEntriesViewModel(settings, service) { ShowFavoritesOnly = false };
        vm.ReloadFavorites();
        Assert.Equal(500, vm.FilteredFavoriteRows.Count);
        Assert.Equal(0, service.ActivityCalls);
        Assert.Equal(0, service.FieldCalls);

        var view = new TimeEntriesView { DataContext = vm };
        var window = new Window { Width = 1100, Height = 900, Content = view };
        try
        {
            window.Show();
            await SettleAsync(window);
            var list = view.FindControl<ListBox>("BookingRows")!;
            Assert.NotNull(list);
            Assert.InRange(list.GetRealizedContainers().Count(), 1, 30);
            Assert.InRange(service.ActivityCalls, 1, 30);
            Assert.InRange(service.MaxConcurrentActivities, 1, 4);
            service.ReleaseActivities();
            await SettleAsync(window);
            Assert.InRange(service.FieldCalls, 1, 30);
            Assert.DoesNotContain(500, service.LoadedIssueIds);

            var row = vm.FilteredFavoriteRows[0];
            var firstContainer = list.ContainerFromItem(row)!;
            Assert.NotNull(firstContainer);
            var hoursInput = firstContainer.GetVisualDescendants().OfType<TextBox>().Single(box => box.Classes.Contains("hours-input"));
            hoursInput.Text = "3,75";
            row.Comments = "draft survives recycled controls";
            row.SpentOn = new DateTime(2026, 10, 27);
            var activity = row.Activities[0];
            row.SelectedActivity = activity;
            var field = Assert.Single(row.CustomFields);
            field.Value = "draft field";
            await SettleAsync(window);
            Assert.Equal("3,75", row.Hours);
            var firstIssueLoads = service.LoadedIssueIds.Count(id => id == 1);

            list.ScrollIntoView(vm.FilteredFavoriteRows[^1]);
            await SettleAsync(window);
            Assert.InRange(list.GetRealizedContainers().Count(), 1, 30);
            Assert.Null(list.ContainerFromItem(row));
            Assert.Contains(500, service.LoadedIssueIds);

            list.ScrollIntoView(row);
            await SettleAsync(window);
            var returnedContainer = list.ContainerFromItem(row)!;
            Assert.NotNull(returnedContainer);
            Assert.Same(row, returnedContainer.DataContext);
            Assert.Equal("3,75", row.Hours);
            Assert.Equal("draft survives recycled controls", row.Comments);
            Assert.Equal(new DateTime(2026, 10, 27), row.SpentOn);
            Assert.Same(activity, row.SelectedActivity);
            Assert.Same(field, Assert.Single(row.CustomFields));
            Assert.Equal("draft field", field.Value);
            Assert.Equal(firstIssueLoads, service.LoadedIssueIds.Count(id => id == 1));
            Assert.InRange(service.ActivityCalls, 1, 60);
            Assert.InRange(service.FieldCalls, 1, 60);
            Assert.InRange(service.MaxConcurrentActivities, 1, 4);
        }
        finally { window.Close(); }
    }

    private static async Task SettleAsync(Window window)
    {
        for (var pass = 0; pass < 8; pass++)
        {
            window.UpdateLayout();
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        }
    }

    private sealed class MemorySettings(AppSettings settings) : IAppSettingsService
    {
        public string SettingsFilePath => "in-memory";
        public AppSettings Load() => settings;
        public void Save(AppSettings value) => settings = value;
        public void Update(Action<AppSettings> configure) => configure(settings);
    }

    private sealed class FormService : ITimeEntryService
    {
        private readonly TaskCompletionSource _activitiesReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _active;
        public int ActivityCalls { get; private set; }
        public int FieldCalls { get; private set; }
        public int MaxConcurrentActivities { get; private set; }
        public List<int> LoadedIssueIds { get; } = [];
        public void ReleaseActivities() => _activitiesReady.TrySetResult();

        public async Task<IReadOnlyList<TimeEntryActivityDto>> GetActivitiesAsync(string baseUrl, string apiKey, int? issueId = null, int? projectId = null, CancellationToken cancellationToken = default)
        {
            ActivityCalls++;
            LoadedIssueIds.Add(issueId!.Value);
            MaxConcurrentActivities = Math.Max(MaxConcurrentActivities, ++_active);
            try
            {
                await _activitiesReady.Task.WaitAsync(cancellationToken);
                return [new() { Id = 1, Name = "Development" }];
            }
            finally { _active--; }
        }

        public Task<IReadOnlyList<TimeEntryCustomFieldValueDto>> GetRecentCustomFieldValuesAsync(string baseUrl, string apiKey, int? issueId = null, int? projectId = null, CancellationToken cancellationToken = default)
        {
            FieldCalls++;
            return Task.FromResult<IReadOnlyList<TimeEntryCustomFieldValueDto>>([new() { Id = 17, Name = "Reference", Value = "initial" }]);
        }

        public Task<TimeEntryLoadResult> GetMyTimeEntriesAsync(string baseUrl, string apiKey, DateTime from, DateTime to, CancellationToken cancellationToken = default) =>
            Task.FromResult(new TimeEntryLoadResult { Success = true });
        public Task<TimeEntryOperationResult> CreateTimeEntryAsync(string baseUrl, string apiKey, TimeEntryCreateRequest request, CancellationToken cancellationToken = default) => throw new InvalidOperationException("No writes permitted in this test.");
        public Task<TimeEntryOperationResult> UpdateTimeEntryAsync(string baseUrl, string apiKey, int timeEntryId, TimeEntryUpdateRequest request, CancellationToken cancellationToken = default) => throw new InvalidOperationException("No writes permitted in this test.");
        public Task<TimeEntryOperationResult> DeleteTimeEntryAsync(string baseUrl, string apiKey, int timeEntryId, CancellationToken cancellationToken = default) => throw new InvalidOperationException("No writes permitted in this test.");
    }
}
