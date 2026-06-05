namespace EasyRedmineTool.Core.Services;

using EasyRedmineTool.Core.Api;
using EasyRedmineTool.Core.Models.Tickets;
using EasyRedmineTool.Core.Services.Interfaces;

public class TicketService(EasyRedmineApiClient apiClient) : ITicketService
{
    private readonly EasyRedmineApiClient _apiClient = apiClient;

    public async Task<IReadOnlyList<IssueDto>> GetMyOpenIssuesAsync(string baseUrl, string apiKey, CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.GetMyOpenIssuesAsync(baseUrl, apiKey, cancellationToken);
        return result?.Issues ?? [];
    }

    public async Task<IssueDto?> GetIssueByIdAsync(string baseUrl, string apiKey, int issueId, CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.GetIssueByIdAsync(baseUrl, apiKey, issueId, cancellationToken);
        return result?.Issue;
    }
}

