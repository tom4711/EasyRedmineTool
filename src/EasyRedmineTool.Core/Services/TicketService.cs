namespace EasyRedmineTool.Core.Services;

using EasyRedmineTool.Core;
using EasyRedmineTool.Core.Api;
using EasyRedmineTool.Core.Models.TimeEntries;
using EasyRedmineTool.Core.Models.Tickets;
using EasyRedmineTool.Core.Services.Interfaces;

public class TicketService(IEasyRedmineApiClient apiClient) : ITicketService
{
    private const int TimeEntryLookbackMonths = 12;
    private static readonly TimeSpan TimeEntryCacheTtl = TimeSpan.FromMinutes(5);

    private readonly IEasyRedmineApiClient _apiClient = apiClient;
    private readonly object _timeEntryCacheLock = new();
    private string? _timeEntryCacheKey;
    private DateTime _timeEntryCacheExpiresAt;
    private IReadOnlyList<TimeEntryDto> _cachedTimeEntries = [];

    public void InvalidateTimeEntryCache()
    {
        lock (_timeEntryCacheLock)
        {
            _timeEntryCacheKey = null;
            _timeEntryCacheExpiresAt = DateTime.MinValue;
            _cachedTimeEntries = [];
        }
    }

    public async Task<IReadOnlyList<IssueDto>> GetMyOpenIssuesAsync(string baseUrl, string apiKey, CancellationToken cancellationToken = default)
    {
        return await _apiClient.GetAllMyOpenIssuesAsync(baseUrl, apiKey, cancellationToken);
    }

    public async Task<TicketListLoadResult> GetTicketsForListAsync(
        string baseUrl,
        string apiKey,
        TicketLoadFilter filter,
        CancellationToken cancellationToken = default)
    {
        var primaryIssues = await _apiClient.GetIssuesAsync(
            baseUrl,
            apiKey,
            filter.Assignee,
            filter.StatusKind,
            filter.StatusId,
            cancellationToken);
        if (!filter.IncludeTimeEntryTickets)
        {
            var primaryOnly = primaryIssues.OrderBy(ticket => ticket.Id).ToList();
            return new TicketListLoadResult
            {
                Tickets = primaryOnly,
                OpenTicketCount = primaryOnly.Count,
                TimeEntryTicketCount = 0
            };
        }

        var currentUserId = await _apiClient.GetCurrentUserIdAsync(baseUrl, apiKey, cancellationToken);
        var knownIds = new HashSet<int>(primaryIssues.Select(i => i.Id));

        var to = DateTime.Today;
        var from = GetTimeEntryFetchFrom(to, filter.LastBookedUntil);
        var timeEntries = await GetTimeEntriesWithCacheAsync(baseUrl, apiKey, from, to, cancellationToken);
        var issueIdsBookedAfterFilter = filter.LastBookedUntil.HasValue
            ? BuildIssueIdsWithSpentOnAfter(timeEntries, filter.LastBookedUntil.Value)
            : null;

        var additionalIssueIds = timeEntries
            .Select(entry => entry.GetIssueId())
            .Where(id => id > 0 && knownIds.Add(id))
            .Distinct()
            .ToList();

        var additionalIssues = additionalIssueIds.Count == 0
            ? []
            : await _apiClient.GetIssuesByIdsAsync(baseUrl, apiKey, additionalIssueIds, cancellationToken);

        var lastTimeEntryByIssue = BuildLastTimeEntryLookup(timeEntries);

        var tickets = primaryIssues
            .Concat(additionalIssues)
            .OrderBy(ticket => ticket.Id)
            .ToList();

        foreach (var ticket in tickets)
        {
            if (lastTimeEntryByIssue.TryGetValue(ticket.Id, out var lastEntryDate))
            {
                ticket.LastTimeEntryOn = lastEntryDate;
            }
        }

        var primaryIds = primaryIssues.Select(issue => issue.Id).ToHashSet();
        var filteredTickets = tickets
            .Where(ticket => primaryIds.Contains(ticket.Id)
                ? TicketLoadFilterMatcher.MatchesLastBookedUntil(
                    ticket,
                    filter.LastBookedUntil,
                    issueIdsBookedAfterFilter)
                : TicketLoadFilterMatcher.Matches(
                    ticket,
                    filter,
                    currentUserId,
                    issueIdsBookedAfterFilter))
            .ToList();

        var primaryTicketCount = filteredTickets.Count(ticket => primaryIds.Contains(ticket.Id));
        var timeEntryTicketCount = filteredTickets.Count - primaryTicketCount;

        return new TicketListLoadResult
        {
            Tickets = filteredTickets,
            OpenTicketCount = primaryTicketCount,
            TimeEntryTicketCount = timeEntryTicketCount
        };
    }

    public async Task<IssueDto?> GetIssueByIdAsync(string baseUrl, string apiKey, int issueId, CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.GetIssueByIdAsync(baseUrl, apiKey, issueId, cancellationToken);
        return result?.Issue;
    }

    public Task<IReadOnlyList<StatusDto>> GetIssueStatusesAsync(
        string baseUrl,
        string apiKey,
        CancellationToken cancellationToken = default) =>
        _apiClient.GetIssueStatusesAsync(baseUrl, apiKey, cancellationToken);

    internal static DateTime GetTimeEntryFetchFrom(DateTime to, DateTime? lastBookedUntil)
    {
        var defaultFrom = to.AddMonths(-TimeEntryLookbackMonths);
        if (!lastBookedUntil.HasValue)
        {
            return defaultFrom;
        }

        var filterFrom = lastBookedUntil.Value.Date;
        return filterFrom < defaultFrom ? filterFrom : defaultFrom;
    }

    internal static HashSet<int> BuildIssueIdsWithSpentOnAfter(
        IReadOnlyList<TimeEntryDto> timeEntries,
        DateTime lastBookedUntil) =>
        timeEntries
            .Select(entry => new
            {
                IssueId = entry.GetIssueId(),
                SpentOn = RedmineDates.TryParseSpentOn(entry.Spent_On)
            })
            .Where(x => x.IssueId > 0
                && x.SpentOn.HasValue
                && x.SpentOn.Value.Date > lastBookedUntil.Date)
            .Select(x => x.IssueId)
            .ToHashSet();

    internal static Dictionary<int, DateTime> BuildLastTimeEntryLookup(IReadOnlyList<TimeEntryDto> timeEntries)
    {
        return timeEntries
            .Select(entry => new
            {
                IssueId = entry.GetIssueId(),
                SpentOn = RedmineDates.TryParseSpentOn(entry.Spent_On)
            })
            .Where(x => x.IssueId > 0 && x.SpentOn.HasValue)
            .GroupBy(x => x.IssueId)
            .ToDictionary(g => g.Key, g => g.Max(x => x.SpentOn!.Value));
    }

    private async Task<IReadOnlyList<TimeEntryDto>> GetTimeEntriesWithCacheAsync(
        string baseUrl,
        string apiKey,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"{baseUrl}|{apiKey}|{RedmineDates.FormatSpentOn(from)}|{RedmineDates.FormatSpentOn(to)}";

        lock (_timeEntryCacheLock)
        {
            if (_timeEntryCacheKey == cacheKey && DateTime.UtcNow < _timeEntryCacheExpiresAt)
            {
                return _cachedTimeEntries;
            }
        }

        var entries = await _apiClient.GetAllMyTimeEntriesAsync(baseUrl, apiKey, from, to, cancellationToken);

        lock (_timeEntryCacheLock)
        {
            _timeEntryCacheKey = cacheKey;
            _timeEntryCacheExpiresAt = DateTime.UtcNow.Add(TimeEntryCacheTtl);
            _cachedTimeEntries = entries;
        }

        return entries;
    }
}
