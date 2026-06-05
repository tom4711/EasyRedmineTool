namespace EasyRedmineTool.Core.Services;

using EasyRedmineTool.Core.Configuration;
using EasyRedmineTool.Core.Services.Interfaces;

using System.Text.Json;

public sealed class AppSettingsService : IAppSettingsService
{
    private const string AppName = "EasyRedmineTool";
    private const string SettingsFileName = "settings.json";
    private const string LegacySettingsFileName = "appsettings.json";
    private const string DefaultBaseUrl = "https://projects.hawe.com/";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _settingsFilePath;
    private readonly string _legacySettingsFilePath;

    public AppSettingsService()
    {
        var settingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppName);

        Directory.CreateDirectory(settingsDirectory);

        _settingsFilePath = Path.Combine(settingsDirectory, SettingsFileName);
        _legacySettingsFilePath = Path.Combine(AppContext.BaseDirectory, LegacySettingsFileName);

        TryMigrateLegacySettings();
    }

    public AppSettings Load()
    {
        if (!File.Exists(_settingsFilePath))
        {
            return CreateDefault();
        }

        return ReadFromFile(_settingsFilePath) ?? CreateDefault();
    }

    public void Save(AppSettings settings)
    {
        var normalized = Normalize(settings);
        var wrapper = new AppSettingsWrapper { AppSettings = normalized };
        var json = JsonSerializer.Serialize(wrapper, JsonOptions);
        File.WriteAllText(_settingsFilePath, json);
    }

    private void TryMigrateLegacySettings()
    {
        if (File.Exists(_settingsFilePath) || !File.Exists(_legacySettingsFilePath))
        {
            return;
        }

        var legacySettings = ReadFromFile(_legacySettingsFilePath);
        if (legacySettings is null)
        {
            return;
        }

        Save(legacySettings);
    }

    private AppSettings? ReadFromFile(string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            var wrapper = JsonSerializer.Deserialize<AppSettingsWrapper>(json, JsonOptions);
            var settings = wrapper?.AppSettings;

            return settings is null ? null : Normalize(settings);
        }
        catch
        {
            return null;
        }
    }

    private static AppSettings Normalize(AppSettings settings) => new()
    {
        BaseUrl = string.IsNullOrWhiteSpace(settings.BaseUrl) ? DefaultBaseUrl : settings.BaseUrl,
        ApiKey = settings.ApiKey ?? string.Empty,
        CachedTickets = settings.CachedTickets ?? [],
        FavoriteTicketIds = settings.FavoriteTicketIds ?? []
    };

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
