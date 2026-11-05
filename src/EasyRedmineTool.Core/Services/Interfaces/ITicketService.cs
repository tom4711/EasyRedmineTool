namespace EasyRedmineTool.Core.Services.Interfaces;

using EasyRedmineTool.Core.Models.Tickets;

public interface ITicketService
{
    Task<IReadOnlyList<ProjectDto>> GetProjectsAsync(
        string baseUrl,
        string apiKey,
        CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProjectDto>>([]);

    Task<IReadOnlyList<IssueDto>> GetProjectTicketsAsync(
        string baseUrl,
        string apiKey,
        int projectId,
        CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<IssueDto>>([]);

    Task<IReadOnlyList<IssueDto>> GetMyOpenIssuesAsync(
        string baseUrl,
        string apiKey,
        CancellationToken cancellationToken = default);

    Task<TicketListLoadResult> GetTicketsForListAsync(
        string baseUrl,
        string apiKey,
        TicketLoadFilter filter,
        CancellationToken cancellationToken = default);

    Task<IssueDto?> GetIssueByIdAsync(
        string baseUrl,
        string apiKey,
        int issueId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StatusDto>> GetIssueStatusesAsync(
        string baseUrl,
        string apiKey,
        CancellationToken cancellationToken = default);

    void InvalidateTimeEntryCache();
}

