namespace EasyRedmineTool.Core.Configuration;

using EasyRedmineTool.Core.Models.Tickets;
using EasyRedmineTool.Core.Services;

using TicketAssigneeFilter = EasyRedmineTool.Core.Models.Tickets.TicketAssigneeFilter;
using TicketStatusFilterKind = EasyRedmineTool.Core.Models.Tickets.TicketStatusFilterKind;

public class AppSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public bool IsDarkMode { get; set; }
    public List<IssueDto> CachedTickets { get; set; } = [];
    public List<int> FavoriteTicketIds { get; set; } = [];
    public List<int> LastLoadedTicketIds { get; set; } = [];
    public TicketAssigneeFilter TicketLoadAssigneeFilter { get; set; } = TicketAssigneeFilter.Me;
    public TicketStatusFilterKind TicketLoadStatusFilterKind { get; set; } = TicketStatusFilterKind.Open;
    public int? TicketLoadStatusId { get; set; }
    public string? TicketLoadStatusName { get; set; }
    public bool TicketLoadIncludeTimeEntryTickets { get; set; }

    public int TicketLoadTimeEntryLookbackMonths { get; set; } = TicketService.DefaultTimeEntryLookbackMonths;

    public List<TimeEntryCustomFieldDefault> TimeEntryCustomFieldDefaults { get; set; } = [];
}
