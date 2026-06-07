namespace EasyRedmineTool.Tests;

using EasyRedmineTool.Core.Configuration;
using EasyRedmineTool.Core.Models;
using EasyRedmineTool.Core.Services;
using EasyRedmineTool.Core.Services.Interfaces;
using EasyRedmineTool.Core.ViewModels;

using Microsoft.Extensions.Logging.Abstractions;

public class SettingsViewModelTests
{
    [Fact]
    public void Constructor_loads_saved_settings()
    {
        using var context = TestContext.Create();
        context.SettingsService.Save(new AppSettings
        {
            BaseUrl = "https://redmine.example/",
            ApiKey = "saved-key"
        });

        var viewModel = new SettingsViewModel(context.ConnectionTestService, context.SettingsService);

        Assert.Equal("https://redmine.example/", viewModel.BaseUrl);
        Assert.Equal("saved-key", viewModel.ApiKey);
    }

    [Fact]
    public void SaveSettings_persists_credentials_and_raises_event()
    {
        using var context = TestContext.Create();
        var viewModel = new SettingsViewModel(context.ConnectionTestService, context.SettingsService)
        {
            BaseUrl = "https://new.example/",
            ApiKey = "new-key"
        };

        var eventRaised = false;
        viewModel.SettingsSaved += (_, _) => eventRaised = true;

        viewModel.SaveSettingsCommand.Execute(null);

        Assert.True(eventRaised);
        Assert.Equal("Einstellungen gespeichert.", viewModel.StatusMessage);

        var loaded = context.SettingsService.Load();
        Assert.Equal("https://new.example/", loaded.BaseUrl);
        Assert.Equal("new-key", loaded.ApiKey);
    }

    [Fact]
    public void ApiKeyStorageHint_contains_settings_file_path()
    {
        using var context = TestContext.Create();
        var viewModel = new SettingsViewModel(context.ConnectionTestService, context.SettingsService);

        Assert.Contains(context.SettingsService.SettingsFilePath, viewModel.ApiKeyStorageHint, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TestConnectionAsync_updates_status_from_service_result()
    {
        using var context = TestContext.Create();
        context.ConnectionTestService.NextResult = new ConnectionTestResult
        {
            Success = true,
            Message = "Verbindung erfolgreich."
        };

        var viewModel = new SettingsViewModel(context.ConnectionTestService, context.SettingsService)
        {
            BaseUrl = "https://redmine.example/",
            ApiKey = "secret"
        };

        await viewModel.TestConnectionCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsBusy);
        Assert.Equal("Verbindung erfolgreich.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task TestConnectionAsync_uses_latest_request_when_called_twice()
    {
        using var context = TestContext.Create();
        context.ConnectionTestService.FirstCallDelay = TimeSpan.FromMilliseconds(150);
        context.ConnectionTestService.NextResult = new ConnectionTestResult
        {
            Success = true,
            Message = "first"
        };
        context.ConnectionTestService.LatestResult = new ConnectionTestResult
        {
            Success = true,
            Message = "second"
        };

        var viewModel = new SettingsViewModel(context.ConnectionTestService, context.SettingsService)
        {
            BaseUrl = "https://redmine.example/",
            ApiKey = "secret"
        };

        var firstCall = viewModel.TestConnectionCommand.ExecuteAsync(null);
        await viewModel.TestConnectionCommand.ExecuteAsync(null);
        await firstCall;

        Assert.Equal("second", viewModel.StatusMessage);
        Assert.Equal(2, context.ConnectionTestService.CallCount);
        Assert.False(viewModel.IsBusy);
    }

    private sealed class TestContext : IDisposable
    {
        private readonly string _settingsPath;

        private TestContext(string settingsPath, AppSettingsService settingsService, FakeConnectionTestService connectionTestService)
        {
            _settingsPath = settingsPath;
            SettingsService = settingsService;
            ConnectionTestService = connectionTestService;
        }

        public AppSettingsService SettingsService { get; }
        public FakeConnectionTestService ConnectionTestService { get; }

        public static TestContext Create()
        {
            var settingsPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
            var settingsService = new AppSettingsService(settingsPath, NullLogger<AppSettingsService>.Instance);
            return new TestContext(settingsPath, settingsService, new FakeConnectionTestService());
        }

        public void Dispose()
        {
            if (File.Exists(_settingsPath))
            {
                File.Delete(_settingsPath);
            }

            var tempPath = _settingsPath + ".tmp";
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private sealed class FakeConnectionTestService : IConnectionTestService
    {
        private int _callCount;

        public TimeSpan FirstCallDelay { get; set; }
        public ConnectionTestResult NextResult { get; set; } = new() { Success = true, Message = "ok" };
        public ConnectionTestResult LatestResult { get; set; } = new() { Success = true, Message = "ok" };
        public int CallCount => _callCount;

        public async Task<ConnectionTestResult> TestConnectionAsync(
            ConnectionTestRequest request,
            CancellationToken cancellationToken = default)
        {
            _callCount++;

            if (_callCount == 1 && FirstCallDelay > TimeSpan.Zero)
            {
                await Task.Delay(FirstCallDelay, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return _callCount == 1 ? NextResult : LatestResult;
        }
    }
}
