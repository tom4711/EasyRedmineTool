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

    public MainWindowViewModel(
        SettingsViewModel settingsViewModel,
        TicketListViewModel ticketListViewModel,
        IAppSettingsService appSettingsService)
    {
        SettingsViewModel = settingsViewModel;
        TicketListViewModel = ticketListViewModel;

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

    [RelayCommand]
    private void OpenSettings()
    {
        ShowSettings();
    }

    private void OnSettingsSaved(object? sender, EventArgs e)
    {
        TicketListViewModel.ReloadSettings();
        ShowTicketList();
    }

    private void ShowSettings()
    {
        IsSettingsVisible = true;
        IsTicketListVisible = false;
    }

    private void ShowTicketList()
    {
        IsSettingsVisible = false;
        IsTicketListVisible = true;
    }
}
