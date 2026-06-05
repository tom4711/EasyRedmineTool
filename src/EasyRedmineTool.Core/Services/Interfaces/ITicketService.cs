namespace EasyRedmineTool.Core.Services.Interfaces;

using EasyRedmineTool.Core.Models.Tickets;

public interface ITicketService
{
    Task<IReadOnlyList<IssueDto>> GetMyOpenIssuesAsync(
        string baseUrl,
        string apiKey,
        CancellationToken cancellationToken = default);
}

