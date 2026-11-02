namespace EasyRedmineTool.Core.Services;

using EasyRedmineTool.Core.Configuration;
using EasyRedmineTool.Core.Models.Tickets;
using EasyRedmineTool.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;

/// <summary>Owns a defensive settings snapshot and a serialized, observed background write queue.</summary>
public sealed class AppSettingsService : IAppSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly object _sync = new();
    private readonly string _settingsFilePath;
    private readonly ILogger<AppSettingsService> _logger;
    private AppSettings _snapshot = CreateDefault();
    private Task? _initialization;
    private Task _writeTail = Task.CompletedTask;
    private bool _pendingChanges;
    private bool _pristine = true;
    private bool _writesBlocked;
    private string? _lastPersistenceError;
    // Accessed only by initialization and then by the serialized writer.
    private readonly Dictionary<string, TicketSnapshot> _writtenTickets = new();
    private readonly Dictionary<string, TicketRevisionHistory> _committedRevisions = new();

    public AppSettingsService(ILogger<AppSettingsService> logger)
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppConstants.SettingsAppName, AppConstants.SettingsFileName), logger) { }

    internal AppSettingsService(string settingsFilePath, ILogger<AppSettingsService> logger)
    {
        _logger = logger;
        _settingsFilePath = settingsFilePath;
    }

    public string SettingsFilePath => _settingsFilePath;
    public string? LastPersistenceError { get { lock (_sync) return _lastPersistenceError; } }
    public event EventHandler<PersistenceFailedEventArgs>? PersistenceFailed;

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Task initialization;
        lock (_sync) initialization = _initialization ??= Task.Run(InitializeCoreAsync, CancellationToken.None);
        // Cancellation belongs to the waiter; another consumer may still need initialization.
        return initialization.WaitAsync(cancellationToken);
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        Task tail;
        lock (_sync) tail = _writeTail;
        await tail.WaitAsync(cancellationToken).ConfigureAwait(false);
        if (LastPersistenceError is { } error) throw new IOException(error);
    }

    public AppSettings Load()
    {
        lock (_sync) return Normalize(_snapshot);
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        lock (_sync) Publish(Normalize(settings), allowInitialSeed: true);
    }

    public void Update(Action<AppSettings> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        lock (_sync)
        {
            var updated = Normalize(_snapshot);
            configure(updated);
            // Clone again because a caller may retain the object passed to its callback.
            Publish(Normalize(updated));
        }
    }

    private void Publish(AppSettings updated, bool allowInitialSeed = false)
    {
        if (!(allowInitialSeed && _pristine) && Identity(_snapshot) != Identity(updated))
        {
            updated.CachedTickets = [];
            updated.FavoriteTicketIds = [];
            updated.LastLoadedTicketIds = [];
            updated.TimeEntryCustomFieldDefaults = [];
        }
        _snapshot = updated;
        _pristine = false;
        _pendingChanges = true;
        _initialization ??= Task.Run(InitializeCoreAsync, CancellationToken.None);
        QueueWrite(updated);
    }

    private async Task InitializeCoreAsync()
    {
        try
        {
            await using var ownership = await AcquirePersistenceLockAsync().ConfigureAwait(false);
            string json;
            try { json = await File.ReadAllTextAsync(_settingsFilePath).ConfigureAwait(false); }
            catch (FileNotFoundException) { return; }
            catch (DirectoryNotFoundException) { return; }

            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("AppSettings", out var settingsElement) ||
                settingsElement.ValueKind != JsonValueKind.Object)
                throw new JsonException("Invalid settings document.");

            var wrapper = JsonSerializer.Deserialize<SettingsDocument>(json, JsonOptions)
                ?? throw new JsonException("Invalid settings document.");
            if (wrapper.SchemaVersion is < 1 or > 3) throw new JsonException("Unsupported settings version.");
            var settings = wrapper.AppSettings ?? throw new JsonException("Missing settings.");
            if (settings.CachedTickets?.Any(ticket => ticket is null) == true ||
                settings.TimeEntryCustomFieldDefaults?.Any(field => field is null) == true)
                throw new JsonException("Invalid settings collection.");
            var loaded = Normalize(settings);
            var identity = Identity(loaded);
            if (wrapper.SchemaVersion >= 2)
            {
                if (wrapper.ConnectionIdentity != identity) throw new JsonException("Connection scope mismatch.");
                var revision = wrapper.SchemaVersion == 3 ? wrapper.TicketCacheRevision : null;
                if (wrapper.SchemaVersion == 3 && !Guid.TryParseExact(revision, "N", out _))
                    throw new JsonException("Invalid ticket cache revision.");
                loaded.CachedTickets = await ReadTicketsAsync(identity, revision).ConfigureAwait(false);
                if (revision is not null)
                {
                    _writtenTickets[identity] = new(loaded.CachedTickets, revision);
                    ImportCommittedHistory(wrapper);
                }
            }

            lock (_sync)
            {
                if (!_pendingChanges)
                {
                    _snapshot = loaded;
                    _pristine = false;
                    // Cache first, then the small document: failed migration leaves the legacy source intact.
                    if (wrapper.SchemaVersion < 3) QueueWrite(loaded);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            lock (_sync) _writesBlocked = true;
            ReportFailure("Einstellungen konnten nicht geladen werden. Die Originaldateien bleiben erhalten; Speichern ist bis zur Wiederherstellung gesperrt.", ex);
        }
    }

    private async Task<List<IssueDto>> ReadTicketsAsync(string identity, string? revision)
    {
        string json;
        try { json = await File.ReadAllTextAsync(TicketPath(identity, revision)).ConfigureAwait(false); }
        catch (FileNotFoundException) when (revision is null) { return []; }
        catch (DirectoryNotFoundException) when (revision is null) { return []; }
        var cache = JsonSerializer.Deserialize<TicketDocument>(json, JsonOptions)
            ?? throw new JsonException("Invalid ticket document.");
        if (cache.ConnectionIdentity != identity || (revision is not null && cache.TicketCacheRevision != revision) ||
            cache.CachedTickets is null || cache.CachedTickets.Any(ticket => ticket is null))
            throw new JsonException("Invalid ticket connection scope.");
        return cache.CachedTickets;
    }

    private void QueueWrite(AppSettings snapshot)
    {
        var preceding = _writeTail;
        _writeTail = Task.Run(async () =>
        {
            // Every queued task observes and handles its failures, even without a Flush caller.
            await preceding.ConfigureAwait(false);
            await _initialization!.ConfigureAwait(false);
            lock (_sync) { if (_writesBlocked) return; }
            try
            {
                await using var ownership = await AcquirePersistenceLockAsync().ConfigureAwait(false);
                await RefreshCommittedHistoryAsync().ConfigureAwait(false);
                var identity = Identity(snapshot);
                if (!_writtenTickets.TryGetValue(identity, out var tickets) || !TicketsEqual(tickets.Tickets, snapshot.CachedTickets) ||
                    !File.Exists(TicketPath(identity, tickets.Revision)))
                {
                    var revision = Guid.NewGuid().ToString("N");
                    await WriteAtomicallyAsync(TicketPath(identity, revision), new TicketDocument
                    {
                        ConnectionIdentity = identity, TicketCacheRevision = revision, CachedTickets = snapshot.CachedTickets
                    }, overwrite: false).ConfigureAwait(false);
                    // Immutable revisions keep the previously committed pair intact if settings fail.
                    // A subsequent queued retry can reuse this fully written, uncommitted revision.
                    tickets = new(snapshot.CachedTickets, revision);
                    _writtenTickets[identity] = tickets;
                }

                _committedRevisions.TryGetValue(identity, out var committed);
                var nextHistory = committed?.Current == tickets.Revision
                    ? committed : new TicketRevisionHistory(tickets.Revision, committed?.Current);
                var histories = new Dictionary<string, TicketRevisionHistory>(_committedRevisions) { [identity] = nextHistory };
                var smallSettings = Normalize(snapshot);
                smallSettings.CachedTickets = [];
                await WriteAtomicallyAsync(_settingsFilePath, new SettingsDocument
                {
                    SchemaVersion = 3, ConnectionIdentity = identity, TicketCacheRevision = tickets.Revision,
                    TicketCacheHistory = histories, AppSettings = smallSettings
                }).ConfigureAwait(false);
                // Only a successful settings replacement advances committed history or allows pruning.
                _committedRevisions[identity] = nextHistory;
                lock (_sync) _lastPersistenceError = null;
                await CleanupRevisionsAsync(identity, nextHistory).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
            {
                ReportFailure("Einstellungen konnten nicht gespeichert werden. Änderungen sind nur in dieser Sitzung verfügbar.", ex);
            }
        });
    }

    private async Task<FileStream> AcquirePersistenceLockAsync()
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(_settingsFilePath))!;
        Directory.CreateDirectory(directory);
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (true)
        {
            try
            {
                // FileShare.None is enforced across processes. Keep the lock file after releasing it:
                // deleting it would allow another writer to lock a different inode at the same path.
                return new FileStream(_settingsFilePath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(20).ConfigureAwait(false);
            }
        }
    }

    private async Task RefreshCommittedHistoryAsync()
    {
        string json;
        try { json = await File.ReadAllTextAsync(_settingsFilePath).ConfigureAwait(false); }
        catch (FileNotFoundException) { _committedRevisions.Clear(); return; }
        var wrapper = JsonSerializer.Deserialize<SettingsDocument>(json, JsonOptions)
            ?? throw new JsonException("Invalid settings document.");
        if (wrapper.SchemaVersion is < 1 or > 3 || wrapper.AppSettings is null)
            throw new JsonException("Invalid settings document.");
        ImportCommittedHistory(wrapper);
    }

    private void ImportCommittedHistory(SettingsDocument wrapper)
    {
        var histories = new Dictionary<string, TicketRevisionHistory>();
        if (wrapper.SchemaVersion == 3)
        {
            var identity = Identity(wrapper.AppSettings!);
            var revision = wrapper.TicketCacheRevision;
            if (wrapper.ConnectionIdentity != identity || !Guid.TryParseExact(revision, "N", out _))
                throw new JsonException("Invalid ticket cache reference.");
            foreach (var (scope, history) in wrapper.TicketCacheHistory ?? [])
            {
                if (scope.Length != 64 || scope.Any(c => !(c is >= '0' and <= '9' or >= 'a' and <= 'f')) ||
                    history is null || !Guid.TryParseExact(history.Current, "N", out _) ||
                    (history.Previous is not null && !Guid.TryParseExact(history.Previous, "N", out _)))
                    throw new JsonException("Invalid ticket cache history.");
                histories[scope] = history;
            }
            if (histories.TryGetValue(identity, out var active) && active.Current != revision)
                throw new JsonException("Ticket cache history mismatch.");
            histories.TryAdd(identity, new(revision!, null));
        }
        _committedRevisions.Clear();
        foreach (var entry in histories) _committedRevisions[entry.Key] = entry.Value;
    }

    private async Task CleanupRevisionsAsync(string identity, TicketRevisionHistory committed)
    {
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(_settingsFilePath))!;
            var prefix = Path.GetFileName(_settingsFilePath) + ".tickets." + identity + ".";
            foreach (var path in Directory.EnumerateFiles(directory))
            {
                var name = Path.GetFileName(path);
                if (name.Length != prefix.Length + 32 + 5 || !name.StartsWith(prefix, StringComparison.Ordinal) ||
                    !name.EndsWith(".json", StringComparison.Ordinal)) continue;
                var revision = name[prefix.Length..^5];
                if (!Guid.TryParseExact(revision, "N", out _) || revision == committed.Current || revision == committed.Previous) continue;
                try
                {
                    // A matching filename alone does not authorize deleting an unrelated/corrupt document.
                    using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path).ConfigureAwait(false));
                    var root = document.RootElement;
                    if (root.ValueKind != JsonValueKind.Object ||
                        !root.TryGetProperty("ConnectionIdentity", out var scope) || scope.ValueKind != JsonValueKind.String || scope.GetString() != identity ||
                        !root.TryGetProperty("TicketCacheRevision", out var storedRevision) || storedRevision.ValueKind != JsonValueKind.String || storedRevision.GetString() != revision)
                        continue;
                    File.Delete(path);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
                {
                    LogCleanupFailure(ex);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            LogCleanupFailure(ex);
        }
    }

    private void LogCleanupFailure(Exception error) =>
        _logger.LogWarning("Obsolete ticket cache cleanup failed ({ErrorType}).", error.GetType().Name);

    private static async Task WriteAtomicallyAsync<T>(string path, T value, bool overwrite = true)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
                await JsonSerializer.SerializeAsync(stream, value, JsonOptions).ConfigureAwait(false);
            File.Move(temporary, path, overwrite);
        }
        finally
        {
            try { File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private void ReportFailure(string message, Exception error)
    {
        lock (_sync) _lastPersistenceError = message;
        // Do not log exception text or settings paths: these can contain user-provided values.
        _logger.LogWarning("Settings persistence failed ({ErrorType}).", error.GetType().Name);
        var subscribers = PersistenceFailed;
        if (subscribers is null) return;
        foreach (EventHandler<PersistenceFailedEventArgs> subscriber in subscribers.GetInvocationList())
        {
            try { subscriber(this, new PersistenceFailedEventArgs(message)); }
            catch (Exception) { _logger.LogWarning("Settings persistence error notification failed."); }
        }
    }

    private string TicketPath(string identity, string? revision) => _settingsFilePath + ".tickets." + identity +
        (revision is null ? string.Empty : "." + revision) + ".json";
    private static string Identity(AppSettings settings) => ConnectionIdentity.Get(settings.BaseUrl, settings.ApiKey);

    internal static AppSettings Normalize(AppSettings settings) => new()
    {
        BaseUrl = settings.BaseUrl?.Trim() ?? string.Empty,
        ApiKey = settings.ApiKey ?? string.Empty,
        IsDarkMode = settings.IsDarkMode,
        CachedTickets = (settings.CachedTickets ?? []).Select(CloneTicket).ToList(),
        FavoriteTicketIds = [.. settings.FavoriteTicketIds ?? []],
        LastLoadedTicketIds = [.. settings.LastLoadedTicketIds ?? []],
        TicketLoadAssigneeFilter = settings.TicketLoadAssigneeFilter,
        TicketLoadStatusFilterKind = settings.TicketLoadStatusFilterKind,
        TicketLoadStatusId = settings.TicketLoadStatusId,
        TicketLoadStatusName = settings.TicketLoadStatusName,
        TicketLoadIncludeTimeEntryTickets = settings.TicketLoadIncludeTimeEntryTickets,
        TicketLoadTimeEntryLookbackMonths = TicketLoadFilterDefaults.NormalizeTimeEntryLookbackMonths(settings.TicketLoadTimeEntryLookbackMonths),
        TimeEntryCustomFieldDefaults = (settings.TimeEntryCustomFieldDefaults ?? []).Select(f => new TimeEntryCustomFieldDefault { Id = f.Id, Name = f.Name, Value = f.Value }).ToList()
    };

    private static IssueDto CloneTicket(IssueDto issue) => new()
    {
        Id = issue.Id, Subject = issue.Subject, Project = CloneEntity(issue.Project),
        Status = issue.Status is null ? null : new StatusDto { Id = issue.Status.Id, Name = issue.Status.Name, Is_Closed = issue.Status.Is_Closed },
        Priority = CloneEntity(issue.Priority), Tracker = CloneEntity(issue.Tracker), Assigned_To = CloneEntity(issue.Assigned_To),
        Due_Date = issue.Due_Date, LastTimeEntryOn = issue.LastTimeEntryOn
    };
    private static NamedEntityDto? CloneEntity(NamedEntityDto? entity) => entity is null ? null : new() { Id = entity.Id, Name = entity.Name };
    private static bool EntityEqual(NamedEntityDto? left, NamedEntityDto? right) => left?.Id == right?.Id && left?.Name == right?.Name;
    private static bool TicketsEqual(List<IssueDto> left, List<IssueDto> right) => left.Count == right.Count && left.Zip(right).All(pair =>
        pair.First.Id == pair.Second.Id && pair.First.Subject == pair.Second.Subject &&
        EntityEqual(pair.First.Project, pair.Second.Project) && EntityEqual(pair.First.Status, pair.Second.Status) &&
        pair.First.Status?.Is_Closed == pair.Second.Status?.Is_Closed && EntityEqual(pair.First.Priority, pair.Second.Priority) &&
        EntityEqual(pair.First.Tracker, pair.Second.Tracker) && EntityEqual(pair.First.Assigned_To, pair.Second.Assigned_To) &&
        pair.First.Due_Date == pair.Second.Due_Date && pair.First.LastTimeEntryOn == pair.Second.LastTimeEntryOn);

    private static AppSettings CreateDefault() => new() { BaseUrl = AppConstants.DefaultBaseUrl };
    private sealed record TicketSnapshot(List<IssueDto> Tickets, string Revision);
    private sealed record TicketRevisionHistory(string Current, string? Previous);

    private sealed class SettingsDocument
    {
        public int SchemaVersion { get; set; } = 1;
        public string? ConnectionIdentity { get; set; }
        public string? TicketCacheRevision { get; set; }
        public Dictionary<string, TicketRevisionHistory>? TicketCacheHistory { get; set; }
        public AppSettings? AppSettings { get; set; }
    }
    private sealed class TicketDocument
    {
        public string? ConnectionIdentity { get; set; }
        public string? TicketCacheRevision { get; set; }
        public List<IssueDto>? CachedTickets { get; set; }
    }
}
