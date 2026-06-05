namespace EasyRedmineTool.Desktop.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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

    public MainWindowViewModel(
        SettingsViewModel settingsViewModel,
        TicketListViewModel ticketListViewModel,
        TimeEntriesViewModel timeEntriesViewModel,
        IAppSettingsService appSettingsService)
    {
        SettingsViewModel = settingsViewModel;
        TicketListViewModel = ticketListViewModel;
        TimeEntriesViewModel = timeEntriesViewModel;

        SettingsViewModel.SettingsSaved += OnSettingsSaved;

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
        ShowTimeEntries();
    }

    private void OnSettingsSaved(object? sender, EventArgs e)
    {
        TicketListViewModel.ReloadSettings();
        TimeEntriesViewModel.ReloadFavorites();
        ShowTicketList();
    }

    private void ShowSettings()
    {
        IsSettingsVisible = true;
        IsTicketListVisible = false;
        IsTimeEntriesVisible = false;
    }

    private void ShowTicketList()
    {
        IsSettingsVisible = false;
        IsTicketListVisible = true;
        IsTimeEntriesVisible = false;
    }

    private void ShowTimeEntries()
    {
        IsSettingsVisible = false;
        IsTicketListVisible = false;
        IsTimeEntriesVisible = true;
    }
}
