namespace EasyRedmineTool.Desktop.Views;

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

public partial class TimeEntriesView : UserControl
{
    public TimeEntriesView()
    {
        InitializeComponent();
    }

    private void OnCalendarSelectedDatesChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DatePickerButton.Flyout is Flyout flyout)
        {
            flyout.Hide();
        }
    }
}
