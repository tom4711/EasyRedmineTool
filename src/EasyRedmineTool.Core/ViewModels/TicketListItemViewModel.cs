namespace EasyRedmineTool.Core.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;

using EasyRedmineTool.Core.Models.Tickets;

using System.Globalization;

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

    public string DetailsLine
    {
        get
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(Ticket.Project?.Name))
            {
                parts.Add(Ticket.Project.Name);
            }

            if (!string.IsNullOrWhiteSpace(Ticket.Status?.Name))
            {
                parts.Add(Ticket.Status.Name);
            }

            if (!string.IsNullOrWhiteSpace(Ticket.Priority?.Name))
            {
                parts.Add(Ticket.Priority.Name);
            }

            if (!string.IsNullOrWhiteSpace(Ticket.Tracker?.Name))
            {
                parts.Add(Ticket.Tracker.Name);
            }

            return string.Join(" · ", parts);
        }
    }

    public bool HasDueDate => TryGetDueDate(out _);

    public string DueDateLabel
    {
        get
        {
            if (!TryGetDueDate(out var dueDate))
            {
                return string.Empty;
            }

            return $"Fällig: {dueDate:dd.MM.yyyy}";
        }
    }

    public string LastTimeEntryLabel =>
        Ticket.LastTimeEntryOn.HasValue
            ? $"Zuletzt gebucht: {Ticket.LastTimeEntryOn:dd.MM.yyyy}"
            : "Noch nicht gebucht";

    private bool TryGetDueDate(out DateTime dueDate)
    {
        dueDate = default;

        if (string.IsNullOrWhiteSpace(Ticket.Due_Date))
        {
            return false;
        }

        return DateTime.TryParseExact(
            Ticket.Due_Date,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out dueDate);
    }
}
