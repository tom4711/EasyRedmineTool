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
    private DateTime selectedQuarterStart = GetStartOfQuarter(DateTime.Today);

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private double weeklyTotalHours;

    [ObservableProperty]
    private double maxWeeklyHours = 1;

    public ObservableCollection<WeeklyHoursRowViewModel> WeeklyHours { get; } = [];

    public bool HasLoadedData { get; private set; }

    public bool IsCurrentQuarter => SelectedQuarterStart == GetStartOfQuarter(DateTime.Today);

    public string LoadActionLabel => HasLoadedData ? "Aktualisieren" : "Daten laden";

    public WeeklySummaryViewModel(IAppSettingsService appSettingsService, ITimeEntryService timeEntryService)
    {
        _appSettingsService = appSettingsService;
        _timeEntryService = timeEntryService;

        PrepareView();
    }

    public event EventHandler<int>? OpenTimeEntryRequested;

    public void PrepareView()
    {
        UpdateQuarterLabel();
        OnPropertyChanged(nameof(IsCurrentQuarter));

        if (!HasLoadedData)
        {
            StatusMessage = $"{CurrentQuarterLabel}: Klicke auf „Daten laden“, um die Auswertung abzurufen.";
        }
    }

    partial void OnSelectedQuarterStartChanged(DateTime value)
    {
        var normalized = GetStartOfQuarter(value);
        if (normalized != value)
        {
            SelectedQuarterStart = normalized;
            return;
        }

        OnPropertyChanged(nameof(IsCurrentQuarter));
        UpdateQuarterLabel();
        CancelReload();
        ClearLoadedData();
        StatusMessage = $"{CurrentQuarterLabel}: Klicke auf „Daten laden“, um die Auswertung abzurufen.";
    }

    [RelayCommand]
    public async Task ReloadWeeklySummaryAsync()
    {
        CancelReload();
        var reloadCts = BeginReload();
        var cancellationToken = reloadCts.Token;

        var settings = _appSettingsService.Load();
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            StatusMessage = $"{GetQuarterName(SelectedQuarterStart)}: API-Key fehlt. Bitte zuerst Einstellungen speichern.";
            ClearLoadedData();
            CompleteReload(reloadCts);
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Auswertung wird geladen …";

            var requestedQuarter = SelectedQuarterStart;
            var (from, to, label) = GetSelectedQuarterRange(requestedQuarter);

            var result = await _timeEntryService.GetMyTimeEntriesAsync(
                settings.BaseUrl,
                settings.ApiKey,
                from,
                to,
                cancellationToken);

            if (cancellationToken.IsCancellationRequested || requestedQuarter != SelectedQuarterStart)
            {
                return;
            }

            if (!result.Success)
            {
                StatusMessage = $"{GetQuarterName(requestedQuarter)}: {result.Message}";
                ClearLoadedData();
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
            HasLoadedData = true;
            OnPropertyChanged(nameof(LoadActionLabel));
            StatusMessage = grouped.Count == 0
                ? $"Keine Zeiteinträge im {GetQuarterName(requestedQuarter)}."
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

    [RelayCommand]
    private void PreviousQuarter() => SelectedQuarterStart = SelectedQuarterStart.AddMonths(-3);

    [RelayCommand]
    private void NextQuarter()
    {
        if (!IsCurrentQuarter)
        {
            SelectedQuarterStart = SelectedQuarterStart.AddMonths(3);
        }
    }

    [RelayCommand]
    private void SelectCurrentQuarter() => SelectedQuarterStart = GetStartOfQuarter(DateTime.Today);

    private void UpdateQuarterLabel() => CurrentQuarterLabel = GetSelectedQuarterRange(SelectedQuarterStart).Label;

    private static (DateTime From, DateTime To, string Label) GetSelectedQuarterRange(DateTime quarterStart)
    {
        var from = GetStartOfQuarter(quarterStart);
        var to = from.AddMonths(3).AddDays(-1);

        return (from, to, $"{GetQuarterName(from)} ({RedmineDates.FormatSpentOn(from)} – {RedmineDates.FormatSpentOn(to)})");
    }

    private static string GetQuarterName(DateTime quarterStart) =>
        $"Q{((quarterStart.Month - 1) / 3) + 1} {quarterStart.Year}";

    private static DateTime GetStartOfQuarter(DateTime date) =>
        new(date.Year, (((date.Month - 1) / 3) * 3) + 1, 1);

    private void ClearLoadedData()
    {
        WeeklyHours.Clear();
        WeeklyTotalHours = 0;
        MaxWeeklyHours = 1;
        HasLoadedData = false;
        OnPropertyChanged(nameof(LoadActionLabel));
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
        var reloadCts = _reloadCts;
        _reloadCts = null;
        if (reloadCts is not null)
        {
            IsBusy = false;
            reloadCts.Cancel();
            reloadCts.Dispose();
        }
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
