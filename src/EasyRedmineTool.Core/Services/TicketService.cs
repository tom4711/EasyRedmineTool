namespace EasyRedmineTool.Core.Services;

using EasyRedmineTool.Core.Api;
using EasyRedmineTool.Core.Models.Tickets;
using EasyRedmineTool.Core.Services.Interfaces;

using System.Globalization;

public class TicketService(EasyRedmineApiClient apiClient) : ITicketService
{
    private const int TimeEntryLookbackMonths = 12;

    private readonly EasyRedmineApiClient _apiClient = apiClient;

    public async Task<IReadOnlyList<IssueDto>> GetMyOpenIssuesAsync(string baseUrl, string apiKey, CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.GetMyOpenIssuesAsync(baseUrl, apiKey, cancellationToken);
        return result?.Issues ?? [];
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
        var timeEntries = await _apiClient.GetAllMyTimeEntriesAsync(baseUrl, apiKey, from, to, cancellationToken);

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

    private static Dictionary<int, DateTime> BuildLastTimeEntryLookup(
        IReadOnlyList<Models.TimeEntries.TimeEntryDto> timeEntries)
    {
        return timeEntries
            .Select(entry => new
            {
                IssueId = entry.GetIssueId(),
                SpentOn = TryParseSpentOn(entry.Spent_On)
            })
            .Where(x => x.IssueId > 0 && x.SpentOn.HasValue)
            .GroupBy(x => x.IssueId)
            .ToDictionary(g => g.Key, g => g.Max(x => x.SpentOn!.Value));
    }

    private static DateTime? TryParseSpentOn(string spentOn)
    {
        if (DateTime.TryParseExact(spentOn, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }

        return null;
    }
}
