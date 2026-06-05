using EasyRedmineTool.Core.Models.Tickets;

namespace EasyRedmineTool.Core.Services.Interfaces;

public interface ITicketService
{
    Task<IReadOnlyList<IssueDto>> GetMyOpenIssuesAsync(
        string baseUrl,
        string apiKey,
        CancellationToken cancellationToken = default);
}

