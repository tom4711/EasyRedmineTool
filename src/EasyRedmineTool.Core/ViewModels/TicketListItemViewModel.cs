namespace EasyRedmineTool.Core.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;

using EasyRedmineTool.Core.Models.Tickets;

public partial class TicketListItemViewModel : ObservableObject
{
    public IssueDto Ticket { get; }

    [ObservableProperty]
    private bool isFavorite;

    public TicketListItemViewModel(IssueDto ticket, bool isFavorite)
    {
        Ticket = ticket;
        IsFavorite = isFavorite;
    }
}
