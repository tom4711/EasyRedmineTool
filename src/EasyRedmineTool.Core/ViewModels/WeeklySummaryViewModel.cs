namespace EasyRedmineTool.Core.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using EasyRedmineTool.Core;
using EasyRedmineTool.Core.Services.Interfaces;

using System.Collections.ObjectModel;

public partial class WeeklySummaryViewModel : ViewModelBase, IDisposable
{
    private readonly IAppSettingsService _appSettingsService;
    private readonly ITimeEntryService _timeEntryService;
    private CancellationTokenSource? _reloadCts;

    [ObservableProperty]
    private string currentQuarterLabel = string.Empty;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private double weeklyTotalHours;

    [ObservableProperty]
    private double maxWeeklyHours = 1;

    public ObservableCollection<WeeklyHoursRowViewModel> WeeklyHours { get; } = [];

    public WeeklySummaryViewModel(IAppSettingsService appSettingsService, ITimeEntryService timeEntryService)
    {
        _appSettingsService = appSettingsService;
        _timeEntryService = timeEntryService;
    }

    public event EventHandler<int>? OpenTimeEntryRequested;

    [RelayCommand]
    public async Task ReloadWeeklySummaryAsync()
    {
        CancelReload();
        var reloadCts = BeginReload();
        var cancellationToken = reloadCts.Token;

        var settings = _appSettingsService.Load();
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            StatusMessage = "API-Key fehlt. Bitte zuerst Einstellungen speichern.";
            WeeklyHours.Clear();
            WeeklyTotalHours = 0;
            MaxWeeklyHours = 1;
            CompleteReload(reloadCts);
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Auswertung wird geladen …";

            var (from, to, label) = GetCurrentQuarterRange();
            CurrentQuarterLabel = label;

            var result = await _timeEntryService.GetMyTimeEntriesAsync(
                settings.BaseUrl,
                settings.ApiKey,
                from,
                to,
                cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (!result.Success)
            {
                StatusMessage = result.Message;
                WeeklyHours.Clear();
                WeeklyTotalHours = 0;
                MaxWeeklyHours = 1;
                return;
            }

            var ticketSubjectsById = settings.CachedTickets.ToDictionary(
                ticket => ticket.Id,
                ticket => ticket.Subject);

            var grouped = WeeklySummaryBuilder.Build(result.Entries, ticketSubjectsById);

            WeeklyHours.Clear();
            foreach (var item in grouped)
            {
                WeeklyHours.Add(item);
            }

            WeeklyTotalHours = grouped.Sum(x => x.Hours);
            MaxWeeklyHours = grouped.Count == 0 ? 1 : Math.Max(1, grouped.Max(x => x.Hours));
            StatusMessage = grouped.Count == 0
                ? "Keine Zeiteinträge im aktuellen Quartal."
                : string.Empty;
        }
        catch (OperationCanceledException)
        {
            // A newer reload superseded this request.
        }
        finally
        {
            CompleteReload(reloadCts);
        }
    }

    private static (DateTime From, DateTime To, string Label) GetCurrentQuarterRange()
    {
        var today = DateTime.Today;
        var quarter = ((today.Month - 1) / 3) + 1;
        var fromMonth = (quarter - 1) * 3 + 1;

        var from = new DateTime(today.Year, fromMonth, 1);
        var to = from.AddMonths(3).AddDays(-1);

        return (from, to, $"Q{quarter} {today.Year} ({RedmineDates.FormatSpentOn(from)} – {RedmineDates.FormatSpentOn(to)})");
    }

    [RelayCommand]
    private void OpenTimeEntryForTicket(int issueId)
    {
        if (issueId <= 0)
        {
            return;
        }

        OpenTimeEntryRequested?.Invoke(this, issueId);
    }

    private void CancelReload()
    {
        _reloadCts?.Cancel();
        _reloadCts?.Dispose();
        _reloadCts = null;
    }

    private CancellationTokenSource BeginReload()
    {
        var reloadCts = new CancellationTokenSource();
        _reloadCts = reloadCts;
        return reloadCts;
    }

    private void CompleteReload(CancellationTokenSource reloadCts)
    {
        if (_reloadCts != reloadCts)
        {
            return;
        }

        IsBusy = false;
        _reloadCts = null;
    }

    public void Dispose()
    {
        CancelReload();
        GC.SuppressFinalize(this);
    }
}
