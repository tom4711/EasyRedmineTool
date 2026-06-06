namespace EasyRedmineTool.Core.Configuration;

using EasyRedmineTool.Core.Models.Tickets;

public class AppSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public List<IssueDto> CachedTickets { get; set; } = [];
    public List<int> FavoriteTicketIds { get; set; } = [];
    public int? LastTimeEntryIssueId { get; set; }
    public int? LastTimeEntryActivityId { get; set; }
    public string LastTimeEntryHours { get; set; } = AppConstants.DefaultHours;
    public string LastTimeEntryActivityName { get; set; } = string.Empty;
}
