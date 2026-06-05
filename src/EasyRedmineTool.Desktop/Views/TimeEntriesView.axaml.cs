namespace EasyRedmineTool.Desktop.Views;

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

using System.Linq;

public partial class TimeEntriesView : UserControl
{
    public TimeEntriesView()
    {
        InitializeComponent();
    }

    private void OnRowCalendarSelectedDatesChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not Calendar calendar)
        {
            return;
        }

        var popup = calendar.GetVisualAncestors().OfType<Popup>().FirstOrDefault();
        popup?.Close();
    }
}
