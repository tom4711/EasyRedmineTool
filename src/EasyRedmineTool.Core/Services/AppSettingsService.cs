using EasyRedmineTool.Core.Configuration;
using EasyRedmineTool.Core.Services.Interfaces;

using System.Text.Json;

namespace EasyRedmineTool.Core.Services;

public sealed class AppSettingsService : IAppSettingsService
{
    private const string DefaultBaseUrl = "https://projects.hawe.com/";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _settingsFilePath;

    public AppSettingsService()
    {
        _settingsFilePath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    }

    public AppSettings Load()
    {
        if (!File.Exists(_settingsFilePath))
        {
            return CreateDefault();
        }

        try
        {
            var json = File.ReadAllText(_settingsFilePath);
            var wrapper = JsonSerializer.Deserialize<AppSettingsWrapper>(json, JsonOptions);
            var settings = wrapper?.AppSettings;

            if (settings is null)
            {
                return CreateDefault();
            }

            if (string.IsNullOrWhiteSpace(settings.BaseUrl))
            {
                settings.BaseUrl = DefaultBaseUrl;
            }

            return settings;
        }
        catch
        {
            return CreateDefault();
        }
    }

    public void Save(AppSettings settings)
    {
        var normalized = new AppSettings
        {
            BaseUrl = string.IsNullOrWhiteSpace(settings.BaseUrl) ? DefaultBaseUrl : settings.BaseUrl,
            ApiKey = settings.ApiKey ?? string.Empty
        };

        var wrapper = new AppSettingsWrapper
        {
            AppSettings = normalized
        };

        var json = JsonSerializer.Serialize(wrapper, JsonOptions);
        File.WriteAllText(_settingsFilePath, json);
    }

    private static AppSettings CreateDefault() => new()
    {
        BaseUrl = DefaultBaseUrl,
        ApiKey = string.Empty
    };

    private sealed class AppSettingsWrapper
    {
        public AppSettings AppSettings { get; set; } = new();
    }
}
