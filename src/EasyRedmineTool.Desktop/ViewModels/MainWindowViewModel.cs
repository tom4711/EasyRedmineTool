namespace EasyRedmineTool.Desktop.ViewModels;

using Avalonia;
using Avalonia.Styling;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using EasyRedmineTool.Core;
using EasyRedmineTool.Core.Services.Interfaces;
using EasyRedmineTool.Core.ViewModels;

using System;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool isSettingsVisible;

    [ObservableProperty]
    private bool isTicketListVisible;

    [ObservableProperty]
    private bool isTimeEntriesVisible;

    [ObservableProperty]
    private bool isAboutVisible;

    public string WindowTitle => AppInfo.WindowTitle;

    public MainWindowViewModel(
        SettingsViewModel settingsViewModel,
        TicketListViewModel ticketListViewModel,
        TimeEntriesViewModel timeEntriesViewModel,
        AboutViewModel aboutViewModel,
        IAppSettingsService appSettingsService)
    {
        SettingsViewModel = settingsViewModel;
        TicketListViewModel = ticketListViewModel;
        TimeEntriesViewModel = timeEntriesViewModel;
        AboutViewModel = aboutViewModel;

        SettingsViewModel.SettingsSaved += OnSettingsSaved;

        if (Application.Current is { } app)
        {
            IsDarkMode = app.RequestedThemeVariant == ThemeVariant.Dark;
        }

        var settings = appSettingsService.Load();
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            ShowSettings();
        }
        else
        {
            ShowTicketList();
        }
    }

    public SettingsViewModel SettingsViewModel { get; }
    public TicketListViewModel TicketListViewModel { get; }
    public TimeEntriesViewModel TimeEntriesViewModel { get; }
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
        ShowTimeEntries();
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
        ShowTicketList();
    }

    private void ShowSettings()
    {
        IsSettingsVisible = true;
        IsTicketListVisible = false;
        IsTimeEntriesVisible = false;
        IsAboutVisible = false;
    }

    private void ShowTicketList()
    {
        IsSettingsVisible = false;
        IsTicketListVisible = true;
        IsTimeEntriesVisible = false;
        IsAboutVisible = false;
    }

    private void ShowTimeEntries()
    {
        IsSettingsVisible = false;
        IsTicketListVisible = false;
        IsTimeEntriesVisible = true;
        IsAboutVisible = false;
    }

    private void ShowAbout()
    {
        IsSettingsVisible = false;
        IsTicketListVisible = false;
        IsTimeEntriesVisible = false;
        IsAboutVisible = true;
    }
}
