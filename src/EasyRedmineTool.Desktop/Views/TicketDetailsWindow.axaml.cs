namespace EasyRedmineTool.Desktop.Views;

using Avalonia.Controls;
using EasyRedmineTool.Core.Models.Tickets;

public partial class TicketDetailsWindow : Window
{
    public TicketDetailsWindow() => InitializeComponent();

    public TicketDetailsWindow(IssueDto issue) : this()
    {
        DataContext = issue;
        Title = $"Ticket #{issue.Id} · {issue.Subject}";
    }

    private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();
}
