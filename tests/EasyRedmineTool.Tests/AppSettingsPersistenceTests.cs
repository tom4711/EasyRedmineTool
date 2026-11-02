namespace EasyRedmineTool.Tests;

using System.Diagnostics;
using System.Text.Json;
using EasyRedmineTool.Core.Configuration;
using EasyRedmineTool.Core.Models.Tickets;
using EasyRedmineTool.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

public sealed class AppSettingsPersistenceTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ert-persistence-" + Guid.NewGuid().ToString("N"));
    private string SettingsPath => Path.Combine(_directory, "settings.json");
    private AppSettingsService Create() => new(SettingsPath, NullLogger<AppSettingsService>.Instance);

    public AppSettingsPersistenceTests(ITestOutputHelper output)
    {
        _output = output;
        Directory.CreateDirectory(_directory);
    }
    public void Dispose() => Directory.Delete(_directory, true);

    [Theory]
    [InlineData("broken json")]
    [InlineData("{}")]
    [InlineData("{\"AppSettings\":null}")]
    [InlineData("{\"AppSettings\":{\"FavoriteTicketIds\":\"invalid\"}}")]
    [InlineData("{\"SchemaVersion\":999,\"AppSettings\":{}}")]
    public async Task Invalid_source_is_preserved_and_blocks_all_automatic_writes(string source)
    {
        await File.WriteAllTextAsync(SettingsPath, source);
        var service = Create();
        await service.InitializeAsync();
        Assert.NotNull(service.LastPersistenceError);
        service.Update(s => s.IsDarkMode = true);
        service.Save(new AppSettings { ApiKey = "replacement" });
        await Assert.ThrowsAsync<IOException>(() => service.FlushAsync());
        Assert.Equal(source, await File.ReadAllTextAsync(SettingsPath));
    }

    [Fact]
    public async Task Unreadable_source_is_not_treated_as_missing()
    {
        Directory.CreateDirectory(SettingsPath);
        var service = Create();
        await service.InitializeAsync();
        Assert.NotNull(service.LastPersistenceError);
        service.Update(s => s.IsDarkMode = true);
        await Assert.ThrowsAsync<IOException>(() => service.FlushAsync());
        Assert.True(Directory.Exists(SettingsPath));
    }

    [Fact]
    public async Task Legacy_settings_migrate_to_separate_cache_and_survive_restart()
    {
        var legacy = new AppSettings { BaseUrl = "https://a.example", ApiKey = "secret", CachedTickets = [new() { Id = 42, Subject = "old" }], FavoriteTicketIds = [42], LastLoadedTicketIds = [42], TimeEntryCustomFieldDefaults = [new() { Id = 2, Value = "value" }] };
        await File.WriteAllTextAsync(SettingsPath, JsonSerializer.Serialize(new { AppSettings = legacy }));
        var service = Create();
        await service.InitializeAsync();
        await service.FlushAsync();
        var settingsText = await File.ReadAllTextAsync(SettingsPath);
        Assert.DoesNotContain("\"Subject\"", settingsText);
        var cachePath = Assert.Single(Directory.GetFiles(_directory, "*.tickets.*.json"));
        Assert.DoesNotContain("secret", Path.GetFileName(cachePath));
        var restarted = Create();
        await restarted.InitializeAsync();
        Assert.Equal("old", Assert.Single(restarted.Load().CachedTickets).Subject);
        Assert.Equal([42], restarted.Load().FavoriteTicketIds);
        Assert.Equal("value", Assert.Single(restarted.Load().TimeEntryCustomFieldDefaults).Value);
    }

    [Fact]
    public async Task Load_and_retained_update_references_cannot_mutate_saved_snapshot()
    {
        var service = Create();
        await service.InitializeAsync();
        var supplied = new AppSettings { BaseUrl = "https://a.example", ApiKey = "secret", CachedTickets = [new() { Id = 42, Project = new() { Name = "original" } }] };
        service.Save(supplied);
        supplied.CachedTickets[0].Project!.Name = "changed outside";
        AppSettings? retained = null;
        service.Update(s => retained = s);
        retained!.CachedTickets[0].Subject = "changed later";
        service.Load().CachedTickets.Clear();
        await service.FlushAsync();
        Assert.Equal("original", Assert.Single(service.Load().CachedTickets).Project!.Name);
        Assert.Equal("", Assert.Single(service.Load().CachedTickets).Subject);
        await File.WriteAllTextAsync(SettingsPath, "broken after initialization");
        Assert.Equal("secret", service.Load().ApiKey);
    }

    [Fact]
    public async Task Small_updates_with_3000_tickets_do_not_touch_ticket_file()
    {
        var service = Create();
        await service.InitializeAsync();
        service.Save(new AppSettings { BaseUrl = "https://a.example", ApiKey = "secret", CachedTickets = Enumerable.Range(1, 3000).Select(id => new IssueDto { Id = id, Subject = "Ticket " + id }).ToList() });
        await service.FlushAsync();
        var cachePath = Assert.Single(Directory.GetFiles(_directory, "*.tickets.*.json"));
        var timestamp = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(cachePath, timestamp);
        var stopwatch = Stopwatch.StartNew();
        service.Update(s => { s.IsDarkMode = true; s.FavoriteTicketIds.Add(42); });
        stopwatch.Stop();
        _output.WriteLine($"3000 tickets: synchronous Update returned in {stopwatch.Elapsed.TotalMilliseconds:F3} ms; ticket cache rewrites for the small update: 0 (timestamp assertion). This measures service return latency, not a UI frame-time guarantee.");
        await service.FlushAsync();
        Assert.Equal(cachePath, Assert.Single(Directory.GetFiles(_directory, "*.tickets.*.json")));
        Assert.Equal(timestamp, File.GetLastWriteTimeUtc(cachePath));
        Assert.DoesNotContain("Ticket 3000", await File.ReadAllTextAsync(SettingsPath));
    }

    [Fact]
    public async Task Concurrent_updates_are_not_lost_in_snapshot_or_final_disk_state()
    {
        var service = Create();
        await service.InitializeAsync();
        await Task.WhenAll(Enumerable.Range(1, 80).Select(id => Task.Run(() => service.Update(s => s.FavoriteTicketIds.Add(id)))));
        await service.FlushAsync();
        Assert.Equal(80, service.Load().FavoriteTicketIds.Count);
        var restarted = Create();
        await restarted.InitializeAsync();
        Assert.Equal(Enumerable.Range(1, 80), restarted.Load().FavoriteTicketIds.Order());
    }

    [Fact]
    public async Task Background_write_failure_is_observed_and_reported_without_throwing_from_update()
    {
        var service = Create();
        await service.InitializeAsync();
        Directory.CreateDirectory(SettingsPath);
        var failed = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.PersistenceFailed += (_, args) => failed.TrySetResult(args.Message);
        service.Update(s => s.ApiKey = "never include this secret");
        await Assert.ThrowsAsync<IOException>(() => service.FlushAsync());
        var error = await failed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.DoesNotContain("never include", error);
        Assert.Equal(error, service.LastPersistenceError);
        Assert.Equal("never include this secret", service.Load().ApiKey);
    }

    [Fact]
    public async Task Different_connections_have_separate_files_and_key_rotation_clears_context()
    {
        var service = Create();
        await service.InitializeAsync();
        service.Save(new AppSettings { BaseUrl = "https://a.example", ApiKey = "key", CachedTickets = [new() { Id = 42, Subject = "A" }], FavoriteTicketIds = [42], LastLoadedTicketIds = [42], TimeEntryCustomFieldDefaults = [new() { Id = 1, Value = "A" }] });
        await service.FlushAsync();
        service.Update(s => s.BaseUrl = "https://b.example");
        Assert.Empty(service.Load().CachedTickets);
        Assert.Empty(service.Load().FavoriteTicketIds);
        Assert.Empty(service.Load().LastLoadedTicketIds);
        Assert.Empty(service.Load().TimeEntryCustomFieldDefaults);
        service.Update(s => s.CachedTickets.Add(new() { Id = 42, Subject = "B" }));
        await service.FlushAsync();
        var aProfile = Assert.Single(Directory.GetFiles(_directory, "settings.json.tickets." + ConnectionIdentity.Get("https://a.example", "key") + ".*.json"));
        Assert.Contains("A", await File.ReadAllTextAsync(aProfile));
        Assert.NotEmpty(Directory.GetFiles(_directory, "settings.json.tickets." + ConnectionIdentity.Get("https://b.example", "key") + ".*.json"));
        var restarted = Create();
        await restarted.InitializeAsync();
        Assert.Equal("B", Assert.Single(restarted.Load().CachedTickets).Subject);
        restarted.Update(s => s.ApiKey = "rotated");
        Assert.Empty(restarted.Load().CachedTickets);
        await restarted.FlushAsync();
    }

    [Fact]
    public async Task Setting_first_key_via_update_clears_data_accumulated_without_credentials()
    {
        var service = Create();
        await service.InitializeAsync();
        service.Update(s => s.FavoriteTicketIds.Add(42));
        service.Update(s => s.ApiKey = "first-key");
        Assert.Empty(service.Load().FavoriteTicketIds);
        await service.FlushAsync();
    }

    [Fact]
    public async Task Malformed_ticket_elements_preserve_legacy_source_instead_of_faulting_initialization()
    {
        const string source = "{\"AppSettings\":{\"CachedTickets\":[null]}}";
        await File.WriteAllTextAsync(SettingsPath, source);
        var service = Create();
        await service.InitializeAsync();
        Assert.NotNull(service.LastPersistenceError);
        service.Update(s => s.IsDarkMode = true);
        await Assert.ThrowsAsync<IOException>(() => service.FlushAsync());
        Assert.Equal(source, await File.ReadAllTextAsync(SettingsPath));
    }

    [Fact]
    public async Task Corrupt_active_ticket_profile_blocks_writes_and_preserves_both_sources()
    {
        var service = Create();
        await service.InitializeAsync();
        service.Save(new AppSettings { ApiKey = "secret", CachedTickets = [new() { Id = 42 }] });
        await service.FlushAsync();
        var settingsSource = await File.ReadAllTextAsync(SettingsPath);
        var cachePath = Assert.Single(Directory.GetFiles(_directory, "*.tickets.*.json"));
        await File.WriteAllTextAsync(cachePath, "broken-cache");
        var restarted = Create();
        await restarted.InitializeAsync();
        Assert.NotNull(restarted.LastPersistenceError);
        restarted.Update(s => s.IsDarkMode = true);
        await Assert.ThrowsAsync<IOException>(() => restarted.FlushAsync());
        Assert.Equal(settingsSource, await File.ReadAllTextAsync(SettingsPath));
        Assert.Equal("broken-cache", await File.ReadAllTextAsync(cachePath));
    }

    [Fact]
    public async Task Initial_save_before_initialization_is_durable_and_defensive()
    {
        var service = Create();
        var supplied = new AppSettings { ApiKey = "initial", FavoriteTicketIds = [42] };
        service.Save(supplied);
        supplied.FavoriteTicketIds.Clear();
        Assert.Equal([42], service.Load().FavoriteTicketIds);
        await service.FlushAsync();
        var restarted = Create();
        await restarted.InitializeAsync();
        Assert.Equal("initial", restarted.Load().ApiKey);
        Assert.Equal([42], restarted.Load().FavoriteTicketIds);
    }

    [Fact]
    public async Task Initial_save_before_initialization_cannot_overwrite_a_corrupt_existing_source()
    {
        await File.WriteAllTextAsync(SettingsPath, "damaged original");
        var service = Create();
        service.Save(new AppSettings { ApiKey = "new" });
        await Assert.ThrowsAsync<IOException>(() => service.FlushAsync());
        Assert.Equal("damaged original", await File.ReadAllTextAsync(SettingsPath));
    }

    [Fact]
    public async Task Failed_small_settings_commit_preserves_previous_ticket_snapshot_and_retry_is_coherent()
    {
        var service = Create();
        await service.InitializeAsync();
        service.Save(new AppSettings { ApiKey = "same-key", CachedTickets = [new() { Id = 42, Subject = "prior" }], FavoriteTicketIds = [42], LastLoadedTicketIds = [42] });
        await service.FlushAsync();
        var previousCache = Assert.Single(Directory.GetFiles(_directory, "*.tickets.*.json"));
        var previousBytes = await File.ReadAllBytesAsync(previousCache);
        var backupPath = SettingsPath + ".committed";
        File.Move(SettingsPath, backupPath);
        Directory.CreateDirectory(SettingsPath);
        try
        {
            // The cache directory remains writable; only the second atomic replacement must fail.
            service.Update(s =>
            {
                s.CachedTickets = [new() { Id = 99, Subject = "replacement" }];
                s.FavoriteTicketIds = [99];
                s.LastLoadedTicketIds = [99];
            });
            await Assert.ThrowsAsync<IOException>(() => service.FlushAsync());
        }
        finally
        {
            Directory.Delete(SettingsPath);
            File.Move(backupPath, SettingsPath);
        }

        var restarted = Create();
        await restarted.InitializeAsync();
        Assert.Null(restarted.LastPersistenceError);
        Assert.Equal(42, Assert.Single(restarted.Load().CachedTickets).Id);
        Assert.Equal([42], restarted.Load().FavoriteTicketIds);
        Assert.Equal([42], restarted.Load().LastLoadedTicketIds);
        Assert.Equal(previousBytes, await File.ReadAllBytesAsync(previousCache));

        service.Update(s => s.IsDarkMode = true);
        await service.FlushAsync();
        Assert.Null(service.LastPersistenceError);
        var retried = Create();
        await retried.InitializeAsync();
        Assert.Equal(99, Assert.Single(retried.Load().CachedTickets).Id);
        Assert.Equal([99], retried.Load().FavoriteTicketIds);
        Assert.Equal([99], retried.Load().LastLoadedTicketIds);
        Assert.True(retried.Load().IsDarkMode);
        Assert.Equal(previousBytes, await File.ReadAllBytesAsync(previousCache));
    }

    [Fact]
    public async Task Intermediate_schema2_cache_migrates_without_losing_context_data()
    {
        var settings = new AppSettings { BaseUrl = "https://a.example", ApiKey = "secret", FavoriteTicketIds = [42], LastLoadedTicketIds = [42] };
        var identity = ConnectionIdentity.Get(settings.BaseUrl, settings.ApiKey);
        var cachePath = SettingsPath + ".tickets." + identity + ".json";
        await File.WriteAllTextAsync(cachePath, JsonSerializer.Serialize(new { ConnectionIdentity = identity, CachedTickets = new[] { new IssueDto { Id = 42, Subject = "schema2" } } }));
        await File.WriteAllTextAsync(SettingsPath, JsonSerializer.Serialize(new { SchemaVersion = 2, ConnectionIdentity = identity, AppSettings = settings }));
        var cacheBytes = await File.ReadAllBytesAsync(cachePath);
        var service = Create();
        await service.InitializeAsync();
        await service.FlushAsync();
        Assert.Equal(cacheBytes, await File.ReadAllBytesAsync(cachePath));
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(SettingsPath));
        Assert.Equal(3, document.RootElement.GetProperty("SchemaVersion").GetInt32());
        Assert.False(string.IsNullOrEmpty(document.RootElement.GetProperty("TicketCacheRevision").GetString()));
        var restarted = Create();
        await restarted.InitializeAsync();
        Assert.Equal("schema2", Assert.Single(restarted.Load().CachedTickets).Subject);
        Assert.Equal([42], restarted.Load().FavoriteTicketIds);
    }

    [Fact]
    public async Task Missing_referenced_ticket_revision_blocks_writes_instead_of_silently_resetting_cache()
    {
        var service = Create();
        await service.InitializeAsync();
        service.Save(new AppSettings { ApiKey = "secret", CachedTickets = [new() { Id = 42 }], FavoriteTicketIds = [42] });
        await service.FlushAsync();
        var previousSettings = await File.ReadAllBytesAsync(SettingsPath);
        var cachePath = Assert.Single(Directory.GetFiles(_directory, "*.tickets.*.json"));
        File.Delete(cachePath);
        var restarted = Create();
        await restarted.InitializeAsync();
        Assert.NotNull(restarted.LastPersistenceError);
        restarted.Update(s => s.IsDarkMode = true);
        await Assert.ThrowsAsync<IOException>(() => restarted.FlushAsync());
        Assert.Equal(previousSettings, await File.ReadAllBytesAsync(SettingsPath));
        Assert.False(File.Exists(cachePath));
    }

    [Fact]
    public async Task Successful_changes_keep_current_and_previous_revisions_across_restart_and_small_updates()
    {
        var service = Create();
        await service.InitializeAsync();
        service.Save(new AppSettings { ApiKey = "same-key", CachedTickets = [new() { Id = 1 }] });
        await service.FlushAsync();
        service.Update(s => s.CachedTickets = [new() { Id = 2 }]);
        await service.FlushAsync();
        service.Update(s => s.CachedTickets = [new() { Id = 3 }]);
        await service.FlushAsync();
        Assert.Equal(2, Directory.GetFiles(_directory, "*.tickets.*.json").Length);

        var restarted = Create();
        await restarted.InitializeAsync();
        restarted.Update(s => s.IsDarkMode = true);
        await restarted.FlushAsync();
        Assert.Equal(2, Directory.GetFiles(_directory, "*.tickets.*.json").Length);
        restarted.Update(s => s.CachedTickets = [new() { Id = 4 }]);
        await restarted.FlushAsync();
        var retained = Directory.GetFiles(_directory, "*.tickets.*.json");
        Assert.Equal(2, retained.Length);
        var retainedIds = new List<int>();
        foreach (var path in retained)
        {
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path));
            retainedIds.Add(document.RootElement.GetProperty("CachedTickets")[0].GetProperty("Id").GetInt32());
        }
        Assert.Equal([3, 4], retainedIds.Order());
    }

    [Fact]
    public async Task Cleanup_preserves_unrelated_names_documents_and_other_connection_profiles()
    {
        var service = Create();
        await service.InitializeAsync();
        service.Save(new AppSettings { BaseUrl = "https://a.example", ApiKey = "key", CachedTickets = [new() { Id = 1 }] });
        await service.FlushAsync();
        var identity = ConnectionIdentity.Get("https://a.example", "key");
        var otherIdentity = ConnectionIdentity.Get("https://b.example", "key");
        var otherRevision = Guid.NewGuid().ToString("N");
        var otherPath = SettingsPath + ".tickets." + otherIdentity + "." + otherRevision + ".json";
        var otherSource = JsonSerializer.Serialize(new { ConnectionIdentity = otherIdentity, TicketCacheRevision = otherRevision, CachedTickets = new[] { new IssueDto { Id = 50 } } });
        var unrelatedPath = SettingsPath + ".tickets." + identity + "." + Guid.NewGuid().ToString("N") + ".json";
        var nonRevisionPath = SettingsPath + ".tickets." + identity + ".notes.json";
        var legacyPath = SettingsPath + ".tickets." + identity + ".json";
        await File.WriteAllTextAsync(otherPath, otherSource);
        await File.WriteAllTextAsync(unrelatedPath, "unrelated document");
        await File.WriteAllTextAsync(nonRevisionPath, "notes");
        await File.WriteAllTextAsync(legacyPath, "legacy source");
        for (var id = 2; id <= 4; id++)
        {
            var nextId = id;
            service.Update(s => s.CachedTickets = [new() { Id = nextId }]);
            await service.FlushAsync();
        }
        Assert.Equal(otherSource, await File.ReadAllTextAsync(otherPath));
        Assert.Equal("unrelated document", await File.ReadAllTextAsync(unrelatedPath));
        Assert.Equal("notes", await File.ReadAllTextAsync(nonRevisionPath));
        Assert.Equal("legacy source", await File.ReadAllTextAsync(legacyPath));
        Assert.Equal(6, Directory.GetFiles(_directory, "*.tickets.*.json").Length);
    }

    [Fact]
    public async Task Failed_commits_preserve_both_committed_revisions_and_retry_prunes_only_after_commit()
    {
        var service = Create();
        await service.InitializeAsync();
        service.Save(new AppSettings { ApiKey = "same-key", CachedTickets = [new() { Id = 1 }], FavoriteTicketIds = [1] });
        await service.FlushAsync();
        service.Update(s => { s.CachedTickets = [new() { Id = 2 }]; s.FavoriteTicketIds = [2]; });
        await service.FlushAsync();
        var committed = Directory.GetFiles(_directory, "*.tickets.*.json");
        Assert.Equal(2, committed.Length);
        var committedSources = committed.ToDictionary(path => path, File.ReadAllText);
        var backupPath = SettingsPath + ".committed";
        File.Move(SettingsPath, backupPath);
        Directory.CreateDirectory(SettingsPath);
        try
        {
            for (var id = 3; id <= 4; id++)
            {
                var nextId = id;
                service.Update(s => { s.CachedTickets = [new() { Id = nextId }]; s.FavoriteTicketIds = [nextId]; });
                await Assert.ThrowsAsync<IOException>(() => service.FlushAsync());
            }
            foreach (var source in committedSources) Assert.Equal(source.Value, await File.ReadAllTextAsync(source.Key));
        }
        finally
        {
            Directory.Delete(SettingsPath);
            File.Move(backupPath, SettingsPath);
        }
        var restarted = Create();
        await restarted.InitializeAsync();
        Assert.Equal(2, Assert.Single(restarted.Load().CachedTickets).Id);
        Assert.Equal([2], restarted.Load().FavoriteTicketIds);
        service.Update(s => s.IsDarkMode = true);
        await service.FlushAsync();
        Assert.Equal(2, Directory.GetFiles(_directory, "*.tickets.*.json").Length);
        var retried = Create();
        await retried.InitializeAsync();
        Assert.Equal(4, Assert.Single(retried.Load().CachedTickets).Id);
        Assert.Equal([4], retried.Load().FavoriteTicketIds);
    }

    [Fact]
    public async Task Two_live_services_refresh_committed_history_before_pruning_or_reusing_a_revision()
    {
        var first = Create();
        await first.InitializeAsync();
        first.Save(new AppSettings { ApiKey = "shared-key", CachedTickets = [new() { Id = 1 }], FavoriteTicketIds = [1] });
        await first.FlushAsync();
        var second = Create();
        await second.InitializeAsync();
        first.Update(s => { s.CachedTickets = [new() { Id = 2 }]; s.FavoriteTicketIds = [2]; });
        await first.FlushAsync();
        second.Update(s => { s.CachedTickets = [new() { Id = 3 }]; s.FavoriteTicketIds = [3]; });
        await second.FlushAsync();
        first.Update(s => s.IsDarkMode = true);
        await first.FlushAsync();
        var restarted = Create();
        await restarted.InitializeAsync();
        Assert.Null(restarted.LastPersistenceError);
        Assert.Equal(2, Assert.Single(restarted.Load().CachedTickets).Id);
        Assert.Equal([2], restarted.Load().FavoriteTicketIds);
        Assert.True(restarted.Load().IsDarkMode);
        Assert.Equal(2, Directory.GetFiles(_directory, "*.tickets.*.json").Length);
    }

    [Fact]
    public void Connection_identity_normalizes_url_but_keeps_path_and_exact_key()
    {
        Assert.Equal(ConnectionIdentity.Get(" HTTPS://EXAMPLE.COM:443/redmine/ ", "key"), ConnectionIdentity.Get("https://example.com/redmine", "key"));
        Assert.NotEqual(ConnectionIdentity.Get("https://example.com/A", "key"), ConnectionIdentity.Get("https://example.com/a", "key"));
        Assert.NotEqual(ConnectionIdentity.Get("https://example.com", "key"), ConnectionIdentity.Get("https://example.com", "key "));
    }
}
