namespace EasyRedmineTool.Desktop.ViewModels;

using Avalonia;
using Avalonia.Styling;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using EasyRedmineTool.Core.Configuration;
using EasyRedmineTool.Core;
using EasyRedmineTool.Core.Services.Interfaces;
using EasyRedmineTool.Core.ViewModels;

using System;
using System.Linq;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IAppSettingsService _appSettingsService;

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

        if (Application.Current is { } app)
        {
            IsDarkMode = app.RequestedThemeVariant == ThemeVariant.Dark;
        }

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
        TimeEntriesViewModel.ReloadFavorites();
        _ = TimeEntriesViewModel.ReloadActivitiesAsync();
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
        TimeEntriesViewModel.ReloadFavorites();
        _ = TimeEntriesViewModel.ReloadActivitiesAsync();
        ShowInitialView();
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
            _ = TimeEntriesViewModel.ReloadActivitiesAsync();
            _ = TimeEntriesViewModel.ReloadTodayBookedHoursAsync();
            ShowTimeEntries();
            return;
        }

        ShowTicketList();
    }

    private static bool HasUsableFavorites(AppSettings settings) =>
        settings.FavoriteTicketIds.Count > 0 &&
        settings.CachedTickets.Any(t => settings.FavoriteTicketIds.Contains(t.Id));

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
