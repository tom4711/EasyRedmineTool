namespace EasyRedmineTool.Desktop.Views;

using System;
using Avalonia.Controls;
using Avalonia.Input;
using EasyRedmineTool.Core.Models.Tickets;
using EasyRedmineTool.Core.ViewModels;

public partial class KanbanDashboardView : UserControl
{
    private bool _isLoadingTicketDetails;

    public KanbanDashboardView() => InitializeComponent();

    private async void OnTicketDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_isLoadingTicketDetails || sender is not Control { DataContext: IssueDto ticket } ||
            DataContext is not KanbanDashboardViewModel viewModel || TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        _isLoadingTicketDetails = true;
        var previousStatusMessage = viewModel.StatusMessage;
        try
        {
            viewModel.StatusMessage = $"Details für Ticket #{ticket.Id} werden geladen …";
            var details = await viewModel.LoadTicketDetailsAsync(ticket.Id);
            if (details is null)
            {
                viewModel.StatusMessage = $"Ticket #{ticket.Id} wurde nicht gefunden oder ist nicht zugänglich.";
                return;
            }

            viewModel.StatusMessage = previousStatusMessage;
            await new TicketDetailsWindow(details).ShowDialog(owner);
        }
        catch (Exception ex)
        {
            viewModel.StatusMessage = $"Details für Ticket #{ticket.Id} konnten nicht geladen werden: {ex.Message}";
        }
        finally
        {
            _isLoadingTicketDetails = false;
        }
    }
}
