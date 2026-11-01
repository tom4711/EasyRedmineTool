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
    private string _savedBaseUrl = string.Empty;
    private string _savedApiKey = string.Empty;
    private string? _lastTestedBaseUrl;
    private string? _lastTestedApiKey;
    private string? _lastTestResultMessage;
    private bool _isInitializing;

    [ObservableProperty]
    private string baseUrl = string.Empty;

    [ObservableProperty]
    private string apiKey = string.Empty;

    [ObservableProperty]
    private string saveStatusMessage = string.Empty;

    [ObservableProperty]
    private string connectionStatusMessage = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool isApiKeyVisible;

    [ObservableProperty]
    private bool hasUnsavedChanges;

    public event EventHandler? SettingsSaved;

    public string ApiKeyStorageWarning =>
        "Der API-Schlüssel wird unverschlüsselt lokal gespeichert. Teilen Sie die Einstellungsdatei nicht und verwenden Sie nur persönliche API-Schlüssel.";

    public string ApiKeyStoragePath => _appSettingsService.SettingsFilePath;

    public char ApiKeyPasswordChar => IsApiKeyVisible ? '\0' : '*';

    public string ApiKeyVisibilityActionLabel => IsApiKeyVisible ? "Schlüssel verbergen" : "Schlüssel anzeigen";

    public SettingsViewModel(IConnectionTestService connectionTestService, IAppSettingsService appSettingsService)
    {
        _connectionTestService = connectionTestService;
        _appSettingsService = appSettingsService;

        var settings = _appSettingsService.Load();
        _isInitializing = true;
        BaseUrl = settings.BaseUrl;
        ApiKey = settings.ApiKey;
        _savedBaseUrl = BaseUrl;
        _savedApiKey = ApiKey;
        _isInitializing = false;
        SaveStatusMessage = "Einstellungen gespeichert.";
    }

    partial void OnBaseUrlChanged(string value) => OnConnectionInputsChanged();

    partial void OnApiKeyChanged(string value) => OnConnectionInputsChanged();

    partial void OnIsApiKeyVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(ApiKeyPasswordChar));
        OnPropertyChanged(nameof(ApiKeyVisibilityActionLabel));
    }

    public void PrepareView() => IsApiKeyVisible = false;

    private void OnConnectionInputsChanged()
    {
        if (_isInitializing)
        {
            return;
        }

        HasUnsavedChanges = BaseUrl != _savedBaseUrl || ApiKey != _savedApiKey;
        if (HasUnsavedChanges)
        {
            SaveStatusMessage = "Ungespeicherte Änderungen.";
        }
        else
        {
            SaveStatusMessage = "Einstellungen gespeichert.";
        }

        if (_connectionTestCts is not null)
        {
            CancelConnectionTest();
            ConnectionStatusMessage = "Eingaben während der Prüfung geändert. Bitte erneut testen.";
        }
        else if (_lastTestedBaseUrl is not null)
        {
            ConnectionStatusMessage = BaseUrl != _lastTestedBaseUrl || ApiKey != _lastTestedApiKey
                ? "Eingaben geändert. Verbindungsergebnis ist veraltet."
                : _lastTestResultMessage ?? string.Empty;
        }
    }

    [RelayCommand]
    private void SaveSettings()
    {
        _appSettingsService.Update(settings =>
        {
            settings.BaseUrl = BaseUrl;
            settings.ApiKey = ApiKey;
        });

        _savedBaseUrl = BaseUrl;
        _savedApiKey = ApiKey;
        HasUnsavedChanges = false;
        SaveStatusMessage = "Einstellungen gespeichert.";
        SettingsSaved?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void ToggleApiKeyVisibility() => IsApiKeyVisible = !IsApiKeyVisible;

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        CancelConnectionTest();
        var testCts = BeginConnectionTest();
        var cancellationToken = testCts.Token;
        var testedBaseUrl = BaseUrl;
        var testedApiKey = ApiKey;

        try
        {
            IsBusy = true;
            ConnectionStatusMessage = "Verbindung wird geprüft ...";

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

            if (BaseUrl != testedBaseUrl || ApiKey != testedApiKey)
            {
                ConnectionStatusMessage = "Eingaben während der Prüfung geändert. Verbindungsergebnis ist veraltet.";
                return;
            }

            _lastTestedBaseUrl = testedBaseUrl;
            _lastTestedApiKey = testedApiKey;
            _lastTestResultMessage = result.Message;
            ConnectionStatusMessage = result.Message;
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
        var testCts = _connectionTestCts;
        _connectionTestCts = null;
        if (testCts is not null)
        {
            IsBusy = false;
            testCts.Cancel();
            testCts.Dispose();
        }
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
        IsApiKeyVisible = false;
        GC.SuppressFinalize(this);
    }
}
