namespace EasyRedmineTool.Core.Models;

public sealed class UpdateCheckResult
{
    public static UpdateCheckResult UpToDate { get; } = new();

    public bool IsUpdateAvailable { get; init; }

    public string? LatestVersion { get; init; }

    public string? ReleaseUrl { get; init; }
}
