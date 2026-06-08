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
using System.Linq;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IAppSettingsService _appSettingsService;
    private bool _isApplyingThemeFromSettings;

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

    public string WindowTitle => AppInfo.WindowTitle;

    public MainWindowViewModel(
        SettingsViewModel settingsViewModel,
        TicketListViewModel ticketListViewModel,
        TimeEntriesViewModel timeEntriesViewModel,
        WeeklySummaryViewModel weeklySummaryViewModel,
        AboutViewModel aboutViewModel,
        IAppSettingsService appSettingsService)
    {
        SettingsViewModel = settingsViewModel;
        TicketListViewModel = ticketListViewModel;
        TimeEntriesViewModel = timeEntriesViewModel;
        WeeklySummaryViewModel = weeklySummaryViewModel;
        AboutViewModel = aboutViewModel;
        _appSettingsService = appSettingsService;

        SettingsViewModel.SettingsSaved += OnSettingsSaved;
        WeeklySummaryViewModel.OpenTimeEntryRequested += OnOpenTimeEntryFromSummary;

        ApplyThemeFromSettings();
        ShowInitialView();
    }

    public SettingsViewModel SettingsViewModel { get; }
    public TicketListViewModel TicketListViewModel { get; }
    public TimeEntriesViewModel TimeEntriesViewModel { get; }
    public WeeklySummaryViewModel WeeklySummaryViewModel { get; }
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
        _ = WeeklySummaryViewModel.ReloadWeeklySummaryAsync();
        ShowWeeklySummary();
    }

    [RelayCommand]
    private void OpenAbout()
    {
        ShowAbout();
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

    private void ShowAbout()
    {
        HideAllViews();
        IsAboutVisible = true;
    }
}
