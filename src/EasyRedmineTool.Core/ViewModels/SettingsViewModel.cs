namespace EasyRedmineTool.Core.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using EasyRedmineTool.Core.Configuration;
using EasyRedmineTool.Core.Models;
using EasyRedmineTool.Core.Services.Interfaces;

public partial class SettingsViewModel : ViewModelBase, IDisposable
{
    private readonly IConnectionTestService _connectionTestService;
    private readonly IAppSettingsService _appSettingsService;
    private CancellationTokenSource? _connectionTestCts;

    [ObservableProperty]
    private string baseUrl = string.Empty;

    [ObservableProperty]
    private string apiKey = string.Empty;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    public event EventHandler? SettingsSaved;

    public string ApiKeyStorageHint =>
        $"Der API-Schlüssel wird unverschlüsselt in {_appSettingsService.SettingsFilePath} gespeichert. Teilen Sie diese Datei nicht und verwenden Sie nur persönliche API-Schlüssel.";

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
        CancelConnectionTest();
        var testCts = BeginConnectionTest();
        var cancellationToken = testCts.Token;

        try
        {
            IsBusy = true;
            StatusMessage = "Verbindung wird geprüft ...";

            var result = await _connectionTestService.TestConnectionAsync(
                new ConnectionTestRequest
                {
                    BaseUrl = BaseUrl,
                    ApiKey = ApiKey
                },
                cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            StatusMessage = result.Message;
        }
        catch (OperationCanceledException)
        {
            // A newer connection test superseded this request.
        }
        finally
        {
            CompleteConnectionTest(testCts);
        }
    }

    private void CancelConnectionTest()
    {
        _connectionTestCts?.Cancel();
        _connectionTestCts?.Dispose();
        _connectionTestCts = null;
    }

    private CancellationTokenSource BeginConnectionTest()
    {
        var testCts = new CancellationTokenSource();
        _connectionTestCts = testCts;
        return testCts;
    }

    private void CompleteConnectionTest(CancellationTokenSource testCts)
    {
        if (_connectionTestCts != testCts)
        {
            return;
        }

        IsBusy = false;
        _connectionTestCts = null;
    }

    public void Dispose()
    {
        CancelConnectionTest();
        GC.SuppressFinalize(this);
    }
}
