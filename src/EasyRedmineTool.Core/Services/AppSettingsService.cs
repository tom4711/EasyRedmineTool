namespace EasyRedmineTool.Core.Services;

using EasyRedmineTool.Core.Configuration;
using EasyRedmineTool.Core.Services.Interfaces;

using System.Text.Json;

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

            settings.BaseUrl = string.IsNullOrWhiteSpace(settings.BaseUrl) ? DefaultBaseUrl : settings.BaseUrl;
            settings.ApiKey ??= string.Empty;
            settings.CachedTickets ??= [];
            settings.FavoriteTicketIds ??= [];

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
            ApiKey = settings.ApiKey ?? string.Empty,
            CachedTickets = settings.CachedTickets ?? [],
            FavoriteTicketIds = settings.FavoriteTicketIds ?? []
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
        ApiKey = string.Empty,
        CachedTickets = [],
        FavoriteTicketIds = []
    };

    private sealed class AppSettingsWrapper
    {
        public AppSettings AppSettings { get; set; } = new();
    }
}
