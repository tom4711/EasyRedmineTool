namespace EasyRedmineTool.Desktop.ViewModels;

using EasyRedmineTool.Core.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public MainWindowViewModel(TicketListViewModel ticketListViewModel)
    {
        TicketListViewModel = ticketListViewModel;
    }

    public TicketListViewModel TicketListViewModel { get; }
}
