namespace EasyRedmineTool.Core.Services.Interfaces;

using EasyRedmineTool.Core.Configuration;

public interface IAppSettingsService
{
    AppSettings Load();
    void Save(AppSettings settings);
}
