namespace EasyRedmineTool.Tests;

using EasyRedmineTool.Core.Configuration;
using EasyRedmineTool.Core.Models.Tickets;
using EasyRedmineTool.Core.Services;

using Microsoft.Extensions.Logging.Abstractions;

public class AppSettingsServiceTests
{
    [Fact]
    public void Normalize_applies_defaults_for_missing_values()
    {
        var settings = new AppSettings
        {
            BaseUrl = "",
            ApiKey = null!,
        };

        var normalized = AppSettingsService.Normalize(settings);

        Assert.Equal(string.Empty, normalized.BaseUrl);
        Assert.Equal(string.Empty, normalized.ApiKey);
        Assert.False(normalized.IsDarkMode);
        Assert.Empty(normalized.CachedTickets);
        Assert.Empty(normalized.FavoriteTicketIds);
    }

    [Fact]
    public void Normalize_preserves_ticket_filters_and_custom_field_defaults()
    {
        var settings = new AppSettings
        {
            BaseUrl = "https://redmine.example/",
            ApiKey = "secret",
            TicketLoadAssigneeFilter = TicketAssigneeFilter.Unassigned,
            TicketLoadStatusFilterKind = TicketStatusFilterKind.Specific,
            TicketLoadStatusId = 7,
            TicketLoadStatusName = "In Progress",
            TicketLoadLastBookedUntil = "2026-05-01",
            TimeEntryCustomFieldDefaults =
            [
                new TimeEntryCustomFieldDefault { Id = 3, Name = "Produktdaten Hierarchie", Value = "A > B" }
            ]
        };

        var normalized = AppSettingsService.Normalize(settings);

        Assert.Equal(TicketAssigneeFilter.Unassigned, normalized.TicketLoadAssigneeFilter);
        Assert.Equal(TicketStatusFilterKind.Specific, normalized.TicketLoadStatusFilterKind);
        Assert.Equal(7, normalized.TicketLoadStatusId);
        Assert.Equal("In Progress", normalized.TicketLoadStatusName);
        Assert.Equal("2026-05-01", normalized.TicketLoadLastBookedUntil);
        Assert.Single(normalized.TimeEntryCustomFieldDefaults);
        Assert.Equal("A > B", normalized.TimeEntryCustomFieldDefaults[0].Value);
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
            });

            service.Update(settings => settings.BaseUrl = "https://new.example/");

            var loaded = service.Load();
            Assert.Equal("https://new.example/", loaded.BaseUrl);
            Assert.Equal("secret", loaded.ApiKey);
            Assert.Equal([42], loaded.FavoriteTicketIds);
        }
        finally
        {
            if (File.Exists(settingsPath))
            {
                File.Delete(settingsPath);
            }
        }
    }

    [Fact]
    public void Save_and_load_round_trip_preserves_ticket_filters_and_custom_field_defaults()
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
        var service = new AppSettingsService(settingsPath, NullLogger<AppSettingsService>.Instance);

        try
        {
            service.Save(new AppSettings
            {
                BaseUrl = "https://redmine.example/",
                ApiKey = "secret",
                TicketLoadAssigneeFilter = TicketAssigneeFilter.All,
                TicketLoadStatusFilterKind = TicketStatusFilterKind.Closed,
                TicketLoadLastBookedUntil = "2026-06-01",
                TimeEntryCustomFieldDefaults =
                [
                    new TimeEntryCustomFieldDefault { Id = 1, Name = "Field", Value = "x" }
                ]
            });

            service.Update(settings => settings.IsDarkMode = true);

            var loaded = service.Load();
            Assert.True(loaded.IsDarkMode);
            Assert.Equal(TicketAssigneeFilter.All, loaded.TicketLoadAssigneeFilter);
            Assert.Equal(TicketStatusFilterKind.Closed, loaded.TicketLoadStatusFilterKind);
            Assert.Equal("2026-06-01", loaded.TicketLoadLastBookedUntil);
            Assert.Single(loaded.TimeEntryCustomFieldDefaults);
            Assert.Equal("x", loaded.TimeEntryCustomFieldDefaults[0].Value);
        }
        finally
        {
            if (File.Exists(settingsPath))
            {
                File.Delete(settingsPath);
            }
        }
    }

    [Fact]
    public void Save_writes_settings_without_leaving_temp_file()
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
        var service = new AppSettingsService(settingsPath, NullLogger<AppSettingsService>.Instance);

        try
        {
            service.Save(new AppSettings
            {
                BaseUrl = "https://redmine.example/",
                ApiKey = "secret",
                IsDarkMode = true
            });

            Assert.True(File.Exists(settingsPath));
            Assert.False(File.Exists(settingsPath + ".tmp"));

            var loaded = service.Load();
            Assert.Equal("https://redmine.example/", loaded.BaseUrl);
            Assert.True(loaded.IsDarkMode);
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
