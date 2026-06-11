namespace EasyRedmineTool.Tests;

using EasyRedmineTool.Core;
using EasyRedmineTool.Core.Api;
using EasyRedmineTool.Core.Models.TimeEntries;
using EasyRedmineTool.Core.Models.Tickets;
using EasyRedmineTool.Core.Services;

using System.Net.Http;

public class TicketServiceTests
{
    [Fact]
    public void GetTimeEntryFetchFrom_uses_default_lookback_without_filter()
    {
        var to = new DateTime(2026, 6, 11);

        var from = TicketService.GetTimeEntryFetchFrom(to, lastBookedUntil: null);

        Assert.Equal(new DateTime(2025, 6, 11), from);
    }

    [Fact]
    public void GetTimeEntryFetchFrom_extends_lookback_when_filter_is_older_than_default_window()
    {
        var to = new DateTime(2026, 6, 11);
        var filter = new DateTime(2025, 1, 1);

        var from = TicketService.GetTimeEntryFetchFrom(to, filter);

        Assert.Equal(filter, from);
    }

    [Fact]
    public void GetTimeEntryFetchFrom_keeps_default_lookback_when_filter_is_within_window()
    {
        var to = new DateTime(2026, 6, 11);
        var filter = new DateTime(2026, 5, 1);

        var from = TicketService.GetTimeEntryFetchFrom(to, filter);

        Assert.Equal(new DateTime(2025, 6, 11), from);
    }

    [Fact]
    public void BuildIssueIdsWithSpentOnAfter_collects_issues_booked_after_filter_date()
    {
        var until = new DateTime(2026, 6, 10);
        var entries = new List<TimeEntryDto>
        {
            CreateEntry(issueId: 1, spentOn: "2026-06-11"),
            CreateEntry(issueId: 2, spentOn: "2026-06-10"),
            CreateEntry(issueId: 3, spentOn: "2026-06-09"),
            CreateEntry(issueId: 0, spentOn: "2026-06-11"),
        };

        var issueIds = TicketService.BuildIssueIdsWithSpentOnAfter(entries, until);

        Assert.Single(issueIds);
        Assert.Contains(1, issueIds);
    }

    [Fact]
    public async Task GetTicketsForListAsync_excludes_unbooked_and_recently_booked_primary_tickets()
    {
        var yesterday = DateTime.Today.AddDays(-1);
        var apiClient = new FilteringTicketApiClient(
            primaryIssues:
            [
                new IssueDto { Id = 1, Subject = "Booked today" },
                new IssueDto { Id = 2, Subject = "Booked yesterday" },
                new IssueDto { Id = 3, Subject = "Never booked" },
            ],
            timeEntries:
            [
                CreateEntry(issueId: 1, spentOn: RedmineDates.FormatSpentOn(DateTime.Today)),
                CreateEntry(issueId: 2, spentOn: RedmineDates.FormatSpentOn(yesterday)),
            ]);
        var service = new TicketService(apiClient);
        var filter = new TicketLoadFilter { LastBookedUntil = yesterday };

        var result = await service.GetTicketsForListAsync("https://redmine.example/", "secret", filter);

        Assert.Single(result.Tickets);
        Assert.Equal(2, result.Tickets[0].Id);
    }

    [Fact]
    public void BuildLastTimeEntryLookup_returns_latest_date_per_issue()
    {
        var entries = new List<TimeEntryDto>
        {
            CreateEntry(issueId: 1, spentOn: "2025-01-10"),
            CreateEntry(issueId: 1, spentOn: "2025-03-01"),
            CreateEntry(issueId: 2, spentOn: "2025-02-15"),
            CreateEntry(issueId: 3, spentOn: "invalid"),
            CreateEntry(issueId: 0, spentOn: "2025-01-01"),
        };

        var lookup = TicketService.BuildLastTimeEntryLookup(entries);

        Assert.Equal(2, lookup.Count);
        Assert.Equal(new DateTime(2025, 3, 1), lookup[1]);
        Assert.Equal(new DateTime(2025, 2, 15), lookup[2]);
    }

    private static TimeEntryDto CreateEntry(int issueId, string spentOn) => new()
    {
        Issue_Id = issueId,
        Spent_On = spentOn,
    };

    private sealed class FilteringTicketApiClient(
        IReadOnlyList<IssueDto> primaryIssues,
        IReadOnlyList<TimeEntryDto> timeEntries) : IEasyRedmineApiClient
    {
        public Task<int?> GetCurrentUserIdAsync(string baseUrl, string apiKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<int?>(1);

        public Task<IReadOnlyList<IssueDto>> GetIssuesAsync(
            string baseUrl,
            string apiKey,
            TicketAssigneeFilter assigneeFilter,
            TicketStatusFilterKind statusKind,
            int? statusId = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(primaryIssues);

        public Task<IReadOnlyList<TimeEntryDto>> GetAllMyTimeEntriesAsync(
            string baseUrl,
            string apiKey,
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(timeEntries);

        public Task<IReadOnlyList<IssueDto>> GetIssuesByIdsAsync(
            string baseUrl,
            string apiKey,
            IReadOnlyList<int> issueIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<IssueDto>>([]);

        public Task<HttpResponseMessage> GetCurrentUserAsync(string baseUrl, string apiKey, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<StatusDto>> GetIssueStatusesAsync(
            string baseUrl,
            string apiKey,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<IssueDto>> GetAllMyOpenIssuesAsync(string baseUrl, string apiKey, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IssueResponse?> GetIssueByIdAsync(string baseUrl, string apiKey, int issueId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<TimeEntryActivityDto>> GetTimeEntryActivitiesAsync(
            string baseUrl,
            string apiKey,
            int? issueId = null,
            int? projectId = null,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<TimeEntryCustomFieldDefinitionDto>> GetAllTimeEntryCustomFieldDefinitionsAsync(
            string baseUrl,
            string apiKey,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<TimeEntryCustomFieldValueDto>> GetRecentTimeEntryCustomFieldValuesAsync(
            string baseUrl,
            string apiKey,
            int? issueId = null,
            int? projectId = null,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<HttpResponseMessage> CreateTimeEntryAsync(
            string baseUrl,
            string apiKey,
            TimeEntryCreateRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<HttpResponseMessage> UpdateTimeEntryAsync(
            string baseUrl,
            string apiKey,
            int timeEntryId,
            TimeEntryUpdateRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<HttpResponseMessage> DeleteTimeEntryAsync(
            string baseUrl,
            string apiKey,
            int timeEntryId,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }
}
