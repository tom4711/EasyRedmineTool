namespace EasyRedmineTool.Core.Services.Interfaces;

using EasyRedmineTool.Core.Configuration;

public interface IAppSettingsService
{
    string SettingsFilePath { get; }

    AppSettings Load();
    void Save(AppSettings settings);
    void Update(Action<AppSettings> configure);
}
