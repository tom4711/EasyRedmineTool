namespace EasyRedmineTool.Core.Configuration;

using EasyRedmineTool.Core.Models.Tickets;

public class AppSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public bool IsDarkMode { get; set; }
    public List<IssueDto> CachedTickets { get; set; } = [];
    public List<int> FavoriteTicketIds { get; set; } = [];
}
