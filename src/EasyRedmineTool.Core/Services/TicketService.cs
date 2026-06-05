using EasyRedmineTool.Core.Api;
using EasyRedmineTool.Core.Models.Tickets;
using EasyRedmineTool.Core.Services.Interfaces;

namespace EasyRedmineTool.Core.Services;

public class TicketService : ITicketService
{
    private readonly EasyRedmineApiClient _apiClient;

    public TicketService(EasyRedmineApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<IReadOnlyList<IssueDto>> GetMyOpenIssuesAsync(
        string baseUrl,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.GetMyOpenIssuesAsync(baseUrl, apiKey, cancellationToken);
        return result?.Issues ?? [];
    }
}

