namespace EasyRedmineTool.Desktop.Views;

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

using System.Linq;

public partial class TicketListView : UserControl
{
    public TicketListView()
    {
        InitializeComponent();
    }

    private void OnLastBookedUntilCalendarSelectedDatesChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not Calendar calendar)
        {
            return;
        }

        var popup = calendar.GetVisualAncestors().OfType<Popup>().FirstOrDefault();
        popup?.Close();
    }
}
