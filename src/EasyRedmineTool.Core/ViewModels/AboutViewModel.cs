namespace EasyRedmineTool.Core.ViewModels;

using CommunityToolkit.Mvvm.Input;

using System.Diagnostics;

public partial class AboutViewModel : ViewModelBase
{
    public string AppName => AppInfo.AppName;

    public string Version => AppInfo.Version;

    public string GitHubUrl => AppInfo.GitHubUrl;

    public IReadOnlyList<LibraryInfo> Libraries => AppInfo.Libraries;

    [RelayCommand]
    private void OpenGitHub()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = AppInfo.GitHubUrl,
            UseShellExecute = true,
        });
    }
}
