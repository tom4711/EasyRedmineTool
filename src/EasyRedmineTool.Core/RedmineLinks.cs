namespace EasyRedmineTool.Core;

using System.Diagnostics;

public static class RedmineLinks
{
    public static string BuildIssueUrl(string baseUrl, int issueId)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return string.Empty;
        }

        return $"{baseUrl.TrimEnd('/')}/issues/{issueId}";
    }

    public static void OpenIssueInBrowser(string baseUrl, int issueId)
    {
        var url = BuildIssueUrl(baseUrl, issueId);
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true,
        });
    }
}
