using EasyRedmineTool.Core.Configuration;

namespace EasyRedmineTool.Core.Services.Interfaces;

public interface IAppSettingsService
{
    AppSettings Load();
    void Save(AppSettings settings);
}
