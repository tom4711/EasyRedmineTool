namespace EasyRedmineTool.Core.Services;

using EasyRedmineTool.Core;
using EasyRedmineTool.Core.Api;
using EasyRedmineTool.Core.Models.TimeEntries;
using EasyRedmineTool.Core.Models.Tickets;
using EasyRedmineTool.Core.Services.Interfaces;

public class TicketService(EasyRedmineApiClient apiClient) : ITicketService
{
    private const int TimeEntryLookbackMonths = 12;
    private static readonly TimeSpan TimeEntryCacheTtl = TimeSpan.FromMinutes(5);

    private readonly EasyRedmineApiClient _apiClient = apiClient;
    private readonly object _timeEntryCacheLock = new();
    private string? _timeEntryCacheKey;
    private DateTime _timeEntryCacheExpiresAt;
    private IReadOnlyList<TimeEntryDto> _cachedTimeEntries = [];

    public async Task<IReadOnlyList<IssueDto>> GetMyOpenIssuesAsync(string baseUrl, string apiKey, CancellationToken cancellationToken = default)
    {
        return await _apiClient.GetAllMyOpenIssuesAsync(baseUrl, apiKey, cancellationToken);
    }

    public async Task<TicketListLoadResult> GetTicketsForListAsync(
        string baseUrl,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        var openIssues = await GetMyOpenIssuesAsync(baseUrl, apiKey, cancellationToken);
        var knownIds = new HashSet<int>(openIssues.Select(i => i.Id));

        var to = DateTime.Today;
        var from = to.AddMonths(-TimeEntryLookbackMonths);
        var timeEntries = await GetTimeEntriesWithCacheAsync(baseUrl, apiKey, from, to, cancellationToken);

        var additionalIssueIds = timeEntries
            .Select(entry => entry.GetIssueId())
            .Where(id => id > 0 && knownIds.Add(id))
            .Distinct()
            .ToList();

        var additionalIssues = additionalIssueIds.Count == 0
            ? []
            : await _apiClient.GetIssuesByIdsAsync(baseUrl, apiKey, additionalIssueIds, cancellationToken);

        var lastTimeEntryByIssue = BuildLastTimeEntryLookup(timeEntries);

        var tickets = openIssues
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

        return new TicketListLoadResult
        {
            Tickets = tickets,
            OpenTicketCount = openIssues.Count,
            TimeEntryTicketCount = additionalIssues.Count
        };
    }

    public async Task<IssueDto?> GetIssueByIdAsync(string baseUrl, string apiKey, int issueId, CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.GetIssueByIdAsync(baseUrl, apiKey, issueId, cancellationToken);
        return result?.Issue;
    }

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
