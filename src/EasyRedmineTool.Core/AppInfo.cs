namespace EasyRedmineTool.Core;

using System.Reflection;
using System.Text.Json;

public static class AppInfo
{
    public const string AppName = "EasyRedmineTool";
    public const string GitHubUrl = "https://github.com/tom4711/EasyRedmineTool";

    public static string Version =>
        GetAppAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? GetAppAssembly().GetName().Version?.ToString()
        ?? "0.0.0";

    public static string DisplayVersion => GetDisplayVersion();

    public const string GitHubReleasesLatestApiUrl =
        "https://api.github.com/repos/tom4711/EasyRedmineTool/releases/latest";

    public static string WindowTitle => $"{AppName} v{GetDisplayVersion()}";

    public static IReadOnlyList<LibraryInfo> Libraries { get; } = LoadLibraries();

    private static string GetDisplayVersion()
    {
        var version = Version;
        var plusIndex = version.IndexOf('+');
        return plusIndex >= 0 ? version[..plusIndex] : version;
    }

    private static Assembly GetAppAssembly() =>
        Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

    private static LibraryInfo[] LoadLibraries()
    {
        try
        {
            var depsPath = FindDependencyManifestPath();
            if (depsPath is null) return [];

            using var document = JsonDocument.Parse(File.ReadAllText(depsPath));
            var root = document.RootElement;
            var targetName = root.GetProperty("runtimeTarget").GetProperty("name").GetString();
            if (string.IsNullOrWhiteSpace(targetName)) return [];

            var target = root.GetProperty("targets").GetProperty(targetName);
            var libraries = root.GetProperty("libraries");
            var packages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var project in target.EnumerateObject())
            {
                if (!libraries.TryGetProperty(project.Name, out var projectLibrary) ||
                    !projectLibrary.TryGetProperty("type", out var projectType) ||
                    projectType.GetString() != "project" ||
                    !project.Value.TryGetProperty("dependencies", out var dependencies))
                {
                    continue;
                }

                foreach (var dependency in dependencies.EnumerateObject())
                {
                    var version = dependency.Value.GetString();
                    if (string.IsNullOrWhiteSpace(version) ||
                        !libraries.TryGetProperty($"{dependency.Name}/{version}", out var packageLibrary) ||
                        !packageLibrary.TryGetProperty("type", out var packageType) ||
                        packageType.GetString() != "package")
                    {
                        continue;
                    }

                    packages[dependency.Name] = version;
                }
            }

            return packages
                .OrderBy(package => package.Key, StringComparer.CurrentCultureIgnoreCase)
                .Select(package => new LibraryInfo(package.Key, package.Value))
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or KeyNotFoundException)
        {
            return [];
        }
    }

    private static string? FindDependencyManifestPath()
    {
        if (AppContext.GetData("APP_CONTEXT_DEPS_FILES") is string depsFiles)
        {
            var existingPath = depsFiles.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(File.Exists);
            if (existingPath is not null) return existingPath;
        }

        var appName = Assembly.GetEntryAssembly()?.GetName().Name;
        if (string.IsNullOrWhiteSpace(appName)) return null;

        var fallbackPath = Path.Combine(AppContext.BaseDirectory, $"{appName}.deps.json");
        return File.Exists(fallbackPath) ? fallbackPath : null;
    }
}

public sealed record LibraryInfo(string Name, string Version);
