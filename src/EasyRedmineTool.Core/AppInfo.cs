namespace EasyRedmineTool.Core;

using System.Reflection;

public static class AppInfo
{
    public const string AppName = "EasyRedmineTool";
    public const string GitHubUrl = "https://github.com/tom4711/EasyRedmineTool";

    public static string Version =>
        GetAppAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? GetAppAssembly().GetName().Version?.ToString()
        ?? "0.0.0";

    public static string WindowTitle => $"{AppName} v{GetDisplayVersion()}";

    public static IReadOnlyList<LibraryInfo> Libraries { get; } =
    [
        new("Avalonia", "12.0.4"),
        new("Avalonia.Desktop", "12.0.4"),
        new("Avalonia.Themes.Fluent", "12.0.4"),
        new("Avalonia.Fonts.Inter", "12.0.4"),
        new("Material.Icons.Avalonia", "3.0.2"),
        new("CommunityToolkit.Mvvm", "8.4.2"),
        new("Microsoft.Extensions.DependencyInjection", "10.0.8"),
        new("Microsoft.Extensions.Http", "10.0.8"),
        new("Microsoft.Extensions.Logging", "10.0.8"),
    ];

    private static string GetDisplayVersion()
    {
        var version = Version;
        var plusIndex = version.IndexOf('+');
        return plusIndex >= 0 ? version[..plusIndex] : version;
    }

    private static Assembly GetAppAssembly() =>
        Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
}

public sealed record LibraryInfo(string Name, string Version);
