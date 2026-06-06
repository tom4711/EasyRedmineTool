namespace EasyRedmineTool.Core.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using EasyRedmineTool.Core;
using EasyRedmineTool.Core.Models;
using EasyRedmineTool.Core.Services.Interfaces;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IConnectionTestService _connectionTestService;
    private readonly IAppSettingsService _appSettingsService;

    [ObservableProperty]
    private string baseUrl = "https://projects.hawe.com/";

    [ObservableProperty]
    private string apiKey = string.Empty;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    public event EventHandler? SettingsSaved;

    public SettingsViewModel(IConnectionTestService connectionTestService, IAppSettingsService appSettingsService)
    {
        _connectionTestService = connectionTestService;
        _appSettingsService = appSettingsService;

        var settings = _appSettingsService.Load();
        BaseUrl = settings.BaseUrl;
        ApiKey = settings.ApiKey;
    }

    [RelayCommand]
    private void SaveSettings()
    {
        _appSettingsService.Update(settings =>
        {
            settings.BaseUrl = BaseUrl;
            settings.ApiKey = ApiKey;
        });

        StatusMessage = "Einstellungen gespeichert.";
        SettingsSaved?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        if (IsBusy)
            return;

        try
        {
            IsBusy = true;
            StatusMessage = "Verbindung wird geprüft ...";

            var result = await _connectionTestService.TestConnectionAsync(new ConnectionTestRequest
            {
                BaseUrl = BaseUrl,
                ApiKey = ApiKey
            });

            StatusMessage = result.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
