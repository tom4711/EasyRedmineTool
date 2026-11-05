namespace EasyRedmineTool.Tests;

using EasyRedmineTool.Core.Configuration;
using EasyRedmineTool.Core.Models.Tickets;
using EasyRedmineTool.Core.Services;

using Microsoft.Extensions.Logging.Abstractions;

public class AppSettingsServiceTests
{
    [Fact]
    public async Task Load_returns_snapshot_without_rereading_disk()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
        var service = new AppSettingsService(path, NullLogger<AppSettingsService>.Instance);
        try
        {
            await service.InitializeAsync();
            service.Save(new AppSettings { ApiKey = "initial" });
            await service.FlushAsync();
            File.WriteAllText(path, "broken");
            Assert.Equal("initial", service.Load().ApiKey);
        }
        finally { foreach (var file in Directory.GetFiles(Path.GetDirectoryName(path)!, Path.GetFileName(path) + "*")) File.Delete(file); }
    }

    [Fact]
    public async Task Connection_change_clears_context_bound_data()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
        var service = new AppSettingsService(path, NullLogger<AppSettingsService>.Instance);
        try
        {
            await service.InitializeAsync();
            service.Save(new AppSettings { BaseUrl = "https://a.example", ApiKey = "key", CachedTickets = [new IssueDto { Id = 42 }], FavoriteTicketIds = [42], LastLoadedTicketIds = [42], TimeEntryCustomFieldDefaults = [new() { Id = 1, Value = "A" }] });
            service.Update(s => s.ApiKey = "rotated");
            await service.FlushAsync();
            var current = service.Load();
            Assert.Empty(current.CachedTickets);
            Assert.Empty(current.FavoriteTicketIds);
            Assert.Empty(current.LastLoadedTicketIds);
            Assert.Empty(current.TimeEntryCustomFieldDefaults);
        }
        finally { foreach (var file in Directory.GetFiles(Path.GetDirectoryName(path)!, Path.GetFileName(path) + "*")) File.Delete(file); }
    }

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
            TicketLoadIncludeTimeEntryTickets = true,
            TicketLoadTimeEntryLookbackMonths = 6,
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
        Assert.True(normalized.TicketLoadIncludeTimeEntryTickets);
        Assert.Equal(6, normalized.TicketLoadTimeEntryLookbackMonths);
        Assert.Single(normalized.TimeEntryCustomFieldDefaults);
        Assert.Equal("A > B", normalized.TimeEntryCustomFieldDefaults[0].Value);
    }

    [Fact]
    public async Task Update_connection_change_preserves_credentials_and_clears_favorites()
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
        var service = new AppSettingsService(settingsPath, NullLogger<AppSettingsService>.Instance);

        try
        {
            await service.InitializeAsync();
            service.Save(new AppSettings
            {
                BaseUrl = "https://old.example/",
                ApiKey = "secret",
                FavoriteTicketIds = [42],
            });

            service.Update(settings => settings.BaseUrl = "https://new.example/");

            await service.FlushAsync();
            var loaded = service.Load();
            Assert.Equal("https://new.example/", loaded.BaseUrl);
            Assert.Equal("secret", loaded.ApiKey);
            Assert.Empty(loaded.FavoriteTicketIds);
        }
        finally
        {
            foreach (var file in Directory.GetFiles(Path.GetDirectoryName(settingsPath)!, Path.GetFileName(settingsPath) + "*")) File.Delete(file);
        }
    }

    [Fact]
    public async Task Save_and_load_round_trip_preserves_ticket_filters_and_custom_field_defaults()
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
        var service = new AppSettingsService(settingsPath, NullLogger<AppSettingsService>.Instance);

        try
        {
            await service.InitializeAsync();
            service.Save(new AppSettings
            {
                BaseUrl = "https://redmine.example/",
                ApiKey = "secret",
                TicketLoadAssigneeFilter = TicketAssigneeFilter.All,
                TicketLoadStatusFilterKind = TicketStatusFilterKind.Closed,
                TicketLoadIncludeTimeEntryTickets = true,
                TicketLoadTimeEntryLookbackMonths = 9,
                TimeEntryCustomFieldDefaults =
                [
                    new TimeEntryCustomFieldDefault { Id = 1, Name = "Field", Value = "x" }
                ]
            });

            service.Update(settings => settings.IsDarkMode = true);

            await service.FlushAsync();
            var loaded = service.Load();
            Assert.True(loaded.IsDarkMode);
            Assert.Equal(TicketAssigneeFilter.All, loaded.TicketLoadAssigneeFilter);
            Assert.Equal(TicketStatusFilterKind.Closed, loaded.TicketLoadStatusFilterKind);
            Assert.True(loaded.TicketLoadIncludeTimeEntryTickets);
            Assert.Equal(9, loaded.TicketLoadTimeEntryLookbackMonths);
            Assert.Single(loaded.TimeEntryCustomFieldDefaults);
            Assert.Equal("x", loaded.TimeEntryCustomFieldDefaults[0].Value);
        }
        finally
        {
            foreach (var file in Directory.GetFiles(Path.GetDirectoryName(settingsPath)!, Path.GetFileName(settingsPath) + "*")) File.Delete(file);
        }
    }

    [Fact]
    public async Task Save_writes_settings_without_leaving_temp_file()
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
        var service = new AppSettingsService(settingsPath, NullLogger<AppSettingsService>.Instance);

        try
        {
            await service.InitializeAsync();
            service.Save(new AppSettings
            {
                BaseUrl = "https://redmine.example/",
                ApiKey = "secret",
                IsDarkMode = true
            });

            await service.FlushAsync();
            Assert.True(File.Exists(settingsPath));
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(settingsPath)!, Path.GetFileName(settingsPath) + "*.tmp"));
            var loaded = service.Load();
            Assert.Equal("https://redmine.example/", loaded.BaseUrl);
            Assert.True(loaded.IsDarkMode);
        }
        finally
        {
            foreach (var file in Directory.GetFiles(Path.GetDirectoryName(settingsPath)!, Path.GetFileName(settingsPath) + "*")) File.Delete(file);
        }
    }
}
