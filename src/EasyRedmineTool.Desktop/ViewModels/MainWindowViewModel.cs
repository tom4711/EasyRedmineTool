namespace EasyRedmineTool.Desktop.ViewModels;

using Avalonia;
using Avalonia.Styling;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using EasyRedmineTool.Core;
using EasyRedmineTool.Core.Configuration;
using EasyRedmineTool.Core.Services.Interfaces;
using EasyRedmineTool.Core.ViewModels;

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly IAppSettingsService _appSettingsService;
    private readonly IUpdateCheckService _updateCheckService;
    private CancellationTokenSource? _updateCheckCts;
    private bool _isApplyingThemeFromSettings;
    private bool _disposed;

    [ObservableProperty]
    private bool isSettingsVisible;

    [ObservableProperty]
    private bool isTicketListVisible;

    [ObservableProperty]
    private bool isTimeEntriesVisible;

    [ObservableProperty]
    private bool isAboutVisible;

    [ObservableProperty]
    private bool isWeeklySummaryVisible;

    [ObservableProperty]
    private bool isSeriesBookingVisible;

    [ObservableProperty]
    private bool isUpdateAvailable;

    [ObservableProperty]
    private string? latestUpdateVersion;

    [ObservableProperty]
    private string? updateReleaseUrl;

    [ObservableProperty]
    private string? updateNoticeDetail;

    public string UpdateBannerMessage =>
        UpdateNoticeDetail ?? UpdateAvailableMessage;

    public string UpdateAvailableMessage =>
        string.IsNullOrWhiteSpace(LatestUpdateVersion)
            ? "Eine neue Version ist verfügbar."
            : $"Version {LatestUpdateVersion} ist verfügbar.";

    public string WindowTitle => AppInfo.WindowTitle;

    public MainWindowViewModel(
        SettingsViewModel settingsViewModel,
        TicketListViewModel ticketListViewModel,
        TimeEntriesViewModel timeEntriesViewModel,
        WeeklySummaryViewModel weeklySummaryViewModel,
        SeriesBookingViewModel seriesBookingViewModel,
        AboutViewModel aboutViewModel,
        IAppSettingsService appSettingsService,
        IUpdateCheckService updateCheckService)
    {
        SettingsViewModel = settingsViewModel;
        TicketListViewModel = ticketListViewModel;
        TimeEntriesViewModel = timeEntriesViewModel;
        WeeklySummaryViewModel = weeklySummaryViewModel;
        SeriesBookingViewModel = seriesBookingViewModel;
        AboutViewModel = aboutViewModel;
        _appSettingsService = appSettingsService;
        _updateCheckService = updateCheckService;

        SettingsViewModel.SettingsSaved += OnSettingsSaved;
        WeeklySummaryViewModel.OpenTimeEntryRequested += OnOpenTimeEntryFromSummary;

        ApplyThemeFromSettings();
        ShowInitialView();
        _ = CheckForUpdatesOnStartupAsync();
    }

    public SettingsViewModel SettingsViewModel { get; }
    public TicketListViewModel TicketListViewModel { get; }
    public TimeEntriesViewModel TimeEntriesViewModel { get; }
    public WeeklySummaryViewModel WeeklySummaryViewModel { get; }
    public SeriesBookingViewModel SeriesBookingViewModel { get; }
    public AboutViewModel AboutViewModel { get; }

    [ObservableProperty]
    private bool _isDarkMode;

    partial void OnIsDarkModeChanged(bool value)
    {
        if (Application.Current is { } app)
        {
            app.RequestedThemeVariant = value ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        if (!_isApplyingThemeFromSettings)
        {
            _appSettingsService.Update(settings => settings.IsDarkMode = value);
        }
    }

    partial void OnLatestUpdateVersionChanged(string? value)
    {
        OnPropertyChanged(nameof(UpdateAvailableMessage));
        OnPropertyChanged(nameof(UpdateBannerMessage));
    }

    partial void OnUpdateNoticeDetailChanged(string? value) =>
        OnPropertyChanged(nameof(UpdateBannerMessage));

    [RelayCommand]
    private void OpenUpdateRelease()
    {
        if (string.IsNullOrWhiteSpace(UpdateReleaseUrl))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = UpdateReleaseUrl,
                UseShellExecute = true,
            });
            UpdateNoticeDetail = null;
        }
        catch (Exception)
        {
            UpdateNoticeDetail = "Release konnte nicht im Browser geöffnet werden.";
        }
    }

    [RelayCommand]
    private void DismissUpdateNotice()
    {
        IsUpdateAvailable = false;
    }

    [RelayCommand]
    private void OpenSettings()
    {
        ShowSettings();
    }

    [RelayCommand]
    private void OpenTicketList()
    {
        TicketListViewModel.ReloadSettings();
        ShowTicketList();
    }

    [RelayCommand]
    private void OpenTimeEntries()
    {
        TimeEntriesViewModel.ClearFocusedIssue();
        TimeEntriesViewModel.ShowFavoritesOnly = true;
        TimeEntriesViewModel.ReloadFavorites();
        _ = TimeEntriesViewModel.ReloadTodayBookedHoursAsync();
        ShowTimeEntries();
    }

    [RelayCommand]
    private void OpenWeeklySummary()
    {
        WeeklySummaryViewModel.PrepareView();
        ShowWeeklySummary();
    }

    [RelayCommand]
    private void OpenSeriesBooking()
    {
        SeriesBookingViewModel.PrepareView();
        ShowSeriesBooking();
    }

    [RelayCommand]
    private void OpenAbout()
    {
        ShowAbout();
    }

    private async Task CheckForUpdatesOnStartupAsync()
    {
        CancelUpdateCheck();
        var updateCheckCts = new CancellationTokenSource();
        _updateCheckCts = updateCheckCts;

        try
        {
            var result = await _updateCheckService.CheckForUpdateAsync(updateCheckCts.Token);
            if (updateCheckCts.IsCancellationRequested || !result.IsUpdateAvailable)
            {
                return;
            }

            IsUpdateAvailable = true;
            LatestUpdateVersion = result.LatestVersion;
            UpdateReleaseUrl = result.ReleaseUrl;
            UpdateNoticeDetail = null;
            OnPropertyChanged(nameof(UpdateAvailableMessage));
            OnPropertyChanged(nameof(UpdateBannerMessage));
        }
        catch (OperationCanceledException)
        {
            // App shutdown or a newer check superseded this request.
        }
        finally
        {
            if (_updateCheckCts == updateCheckCts)
            {
                _updateCheckCts = null;
            }

            updateCheckCts.Dispose();
        }
    }

    private void CancelUpdateCheck()
    {
        _updateCheckCts?.Cancel();
        _updateCheckCts?.Dispose();
        _updateCheckCts = null;
    }

    private void OnSettingsSaved(object? sender, EventArgs e)
    {
        TicketListViewModel.ReloadSettings();
        TimeEntriesViewModel.ClearFocusedIssue();
        TimeEntriesViewModel.ReloadFavorites();
        _ = TimeEntriesViewModel.ReloadTodayBookedHoursAsync();
        ShowInitialView();
    }

    private void OnOpenTimeEntryFromSummary(object? sender, int issueId)
    {
        TimeEntriesViewModel.PrepareForIssue(issueId);
        _ = TimeEntriesViewModel.ReloadTodayBookedHoursAsync();
        ShowTimeEntries();
    }

    private void ShowInitialView()
    {
        var settings = _appSettingsService.Load();
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            ShowSettings();
            return;
        }

        if (HasUsableFavorites(settings))
        {
            TimeEntriesViewModel.ReloadFavorites();
            _ = TimeEntriesViewModel.ReloadTodayBookedHoursAsync();
            ShowTimeEntries();
            return;
        }

        ShowTicketList();
    }

    private static bool HasUsableFavorites(AppSettings settings) =>
        settings.FavoriteTicketIds.Count > 0 &&
        settings.CachedTickets.Any(t => settings.FavoriteTicketIds.Contains(t.Id));

    private void ApplyThemeFromSettings()
    {
        var settings = _appSettingsService.Load();

        _isApplyingThemeFromSettings = true;
        IsDarkMode = settings.IsDarkMode;
        _isApplyingThemeFromSettings = false;
    }

    private void HideAllViews()
    {
        IsSettingsVisible = false;
        IsTicketListVisible = false;
        IsTimeEntriesVisible = false;
        IsWeeklySummaryVisible = false;
        IsSeriesBookingVisible = false;
        IsAboutVisible = false;
    }

    private void ShowSettings()
    {
        HideAllViews();
        IsSettingsVisible = true;
    }

    private void ShowTicketList()
    {
        HideAllViews();
        IsTicketListVisible = true;
    }

    private void ShowTimeEntries()
    {
        HideAllViews();
        IsTimeEntriesVisible = true;
    }

    private void ShowWeeklySummary()
    {
        HideAllViews();
        IsWeeklySummaryVisible = true;
    }

    private void ShowSeriesBooking()
    {
        HideAllViews();
        IsSeriesBookingVisible = true;
    }

    private void ShowAbout()
    {
        HideAllViews();
        IsAboutVisible = true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CancelUpdateCheck();
        SettingsViewModel.SettingsSaved -= OnSettingsSaved;
        WeeklySummaryViewModel.OpenTimeEntryRequested -= OnOpenTimeEntryFromSummary;

        SettingsViewModel.Dispose();
        TicketListViewModel.Dispose();
        TimeEntriesViewModel.Dispose();
        WeeklySummaryViewModel.Dispose();
        SeriesBookingViewModel.Dispose();
    }
}
