namespace EasyRedmineTool.Core.Services;

using EasyRedmineTool.Core.Configuration;
using EasyRedmineTool.Core.Services.Interfaces;

using Microsoft.Extensions.Logging;

using System.Text.Json;

public sealed class AppSettingsService : IAppSettingsService
{
    private const string AppName = AppConstants.SettingsAppName;
    private const string SettingsFileName = AppConstants.SettingsFileName;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _settingsFilePath;
    private readonly ILogger<AppSettingsService> _logger;

    public AppSettingsService(ILogger<AppSettingsService> logger)
    {
        _logger = logger;

        var settingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppName);

        Directory.CreateDirectory(settingsDirectory);

        _settingsFilePath = Path.Combine(settingsDirectory, SettingsFileName);
    }

    internal AppSettingsService(string settingsFilePath, ILogger<AppSettingsService> logger)
    {
        _logger = logger;
        _settingsFilePath = settingsFilePath;
    }

    public string SettingsFilePath => _settingsFilePath;

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

        var directory = Path.GetDirectoryName(_settingsFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = _settingsFilePath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _settingsFilePath, overwrite: true);
    }

    public void Update(Action<AppSettings> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var settings = Load();
        configure(settings);
        Save(settings);
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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Einstellungsdatei konnte nicht gelesen werden: {FilePath}", filePath);
            return null;
        }
    }

    internal static AppSettings Normalize(AppSettings settings) => new()
    {
        BaseUrl = settings.BaseUrl?.Trim() ?? string.Empty,
        ApiKey = settings.ApiKey ?? string.Empty,
        IsDarkMode = settings.IsDarkMode,
        CachedTickets = settings.CachedTickets ?? [],
        FavoriteTicketIds = settings.FavoriteTicketIds ?? [],
        LastLoadedTicketIds = settings.LastLoadedTicketIds ?? [],
    };

    private static AppSettings CreateDefault() => new()
    {
        BaseUrl = AppConstants.DefaultBaseUrl,
        ApiKey = string.Empty,
        IsDarkMode = false,
        CachedTickets = [],
        FavoriteTicketIds = [],
        LastLoadedTicketIds = []
    };

    private sealed class AppSettingsWrapper
    {
        public AppSettings AppSettings { get; set; } = new();
    }
}
