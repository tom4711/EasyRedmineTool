namespace EasyRedmineTool.Desktop.Views;

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

using System.Linq;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using EasyRedmineTool.Core.ViewModels;

public partial class TimeEntriesView : UserControl
{
    public TimeEntriesView()
    {
        InitializeComponent();
    }

    private readonly ConditionalWeakTable<Control, CancellationTokenSource> _cardLoads = new();

    private void OnBookingCardLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is Control card) ActivateCard(card);
    }

    private void OnBookingCardDataContextChanged(object? sender, EventArgs e)
    {
        if (sender is Control { IsLoaded: true } card) ActivateCard(card);
    }

    private void OnBookingCardUnloaded(object? sender, RoutedEventArgs e)
    {
        if (sender is Control card) CancelCard(card);
    }

    private void CancelCard(Control card)
    {
        if (_cardLoads.TryGetValue(card, out var cts))
        {
            _cardLoads.Remove(card);
            cts.Cancel();
            cts.Dispose();
        }
    }

    private void ActivateCard(Control card)
    {
        CancelCard(card);
        if (card.DataContext is not FavoriteTimeEntryRowViewModel row) return;
        var cts = new CancellationTokenSource();
        _cardLoads.Add(card, cts);
        _ = row.EnsureFormLoadedAsync(cts.Token);
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
