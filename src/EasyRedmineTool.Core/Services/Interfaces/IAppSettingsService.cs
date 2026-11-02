namespace EasyRedmineTool.Core.Services.Interfaces;

using EasyRedmineTool.Core.Configuration;

public interface IAppSettingsService
{
    string SettingsFilePath { get; }
    string? LastPersistenceError => null;
    event EventHandler<PersistenceFailedEventArgs>? PersistenceFailed { add { } remove { } }

    Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    Task FlushAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    AppSettings Load();
    void Save(AppSettings settings);
    void Update(Action<AppSettings> configure);
}
