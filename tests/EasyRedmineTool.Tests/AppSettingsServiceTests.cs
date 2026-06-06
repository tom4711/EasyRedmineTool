namespace EasyRedmineTool.Tests;

using EasyRedmineTool.Core.Configuration;
using EasyRedmineTool.Core.Services;

using Microsoft.Extensions.Logging.Abstractions;

public class AppSettingsServiceTests
{
    [Fact]
    public void Normalize_preserves_last_time_entry_fields()
    {
        var settings = new AppSettings
        {
            BaseUrl = "https://example.redmine/",
            ApiKey = "secret",
            FavoriteTicketIds = [42],
            LastTimeEntryIssueId = 42,
            LastTimeEntryActivityId = 7,
            LastTimeEntryHours = "2.5",
            LastTimeEntryActivityName = "Development",
        };

        var normalized = AppSettingsService.Normalize(settings);

        Assert.Equal(42, normalized.LastTimeEntryIssueId);
        Assert.Equal(7, normalized.LastTimeEntryActivityId);
        Assert.Equal("2.5", normalized.LastTimeEntryHours);
        Assert.Equal("Development", normalized.LastTimeEntryActivityName);
    }

    [Fact]
    public void Normalize_applies_defaults_for_missing_values()
    {
        var settings = new AppSettings
        {
            BaseUrl = "",
            ApiKey = null!,
            LastTimeEntryHours = "",
            LastTimeEntryActivityName = null!,
        };

        var normalized = AppSettingsService.Normalize(settings);

        Assert.Equal("https://projects.hawe.com/", normalized.BaseUrl);
        Assert.Equal(string.Empty, normalized.ApiKey);
        Assert.Equal("1", normalized.LastTimeEntryHours);
        Assert.Equal(string.Empty, normalized.LastTimeEntryActivityName);
    }

    [Fact]
    public void Update_mutates_settings_without_dropping_other_fields()
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
        var service = new AppSettingsService(settingsPath, NullLogger<AppSettingsService>.Instance);

        try
        {
            service.Save(new AppSettings
            {
                BaseUrl = "https://old.example/",
                ApiKey = "secret",
                FavoriteTicketIds = [42],
                LastTimeEntryIssueId = 42,
                LastTimeEntryActivityId = 7,
                LastTimeEntryHours = "2.5",
                LastTimeEntryActivityName = "Development",
            });

            service.Update(settings => settings.BaseUrl = "https://new.example/");

            var loaded = service.Load();
            Assert.Equal("https://new.example/", loaded.BaseUrl);
            Assert.Equal("secret", loaded.ApiKey);
            Assert.Equal(42, loaded.LastTimeEntryIssueId);
            Assert.Equal(7, loaded.LastTimeEntryActivityId);
            Assert.Equal("2.5", loaded.LastTimeEntryHours);
            Assert.Equal("Development", loaded.LastTimeEntryActivityName);
        }
        finally
        {
            if (File.Exists(settingsPath))
            {
                File.Delete(settingsPath);
            }
        }
    }
}
