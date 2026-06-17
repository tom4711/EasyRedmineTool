namespace EasyRedmineTool.Core;

public static class AppVersion
{
    public static string Current => AppInfo.DisplayVersion;

    public static bool IsNewer(string latestVersion, string currentVersion)
    {
        if (!TryParse(latestVersion, out var latest) || !TryParse(currentVersion, out var current))
        {
            return false;
        }

        return latest > current;
    }

    public static bool TryParse(string? value, out Version version)
    {
        version = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var cleaned = value.Trim();
        if (cleaned.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned[1..];
        }

        var plusIndex = cleaned.IndexOf('+');
        if (plusIndex >= 0)
        {
            cleaned = cleaned[..plusIndex];
        }

        var dashIndex = cleaned.IndexOf('-');
        if (dashIndex >= 0)
        {
            cleaned = cleaned[..dashIndex];
        }

        return Version.TryParse(cleaned, out version!);
    }
}
