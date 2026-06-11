namespace EasyRedmineTool.Core.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;

using System.Globalization;

public partial class SeriesBookingPreviewRowViewModel : ViewModelBase
{
    private readonly Action? _onSelectionChanged;

    public SeriesBookingPreviewRowViewModel(DateTime date, double hours, bool hasConflict, Action? onSelectionChanged = null)
    {
        Date = date;
        Hours = hours;
        HasConflict = hasConflict;
        IsSelected = !hasConflict;
        _onSelectionChanged = onSelectionChanged;
    }

    public DateTime Date { get; }

    public double Hours { get; }

    [ObservableProperty]
    private bool isSelected;

    public bool HasConflict { get; }

    public string DateLabel => Date.ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("de-DE"));

    public string DayOfWeekLabel => Date.ToString("dddd", CultureInfo.GetCultureInfo("de-DE"));

    public string StatusLabel => HasConflict ? "Bereits gebucht" : "Neu";

    partial void OnIsSelectedChanged(bool value) => _onSelectionChanged?.Invoke();
}
