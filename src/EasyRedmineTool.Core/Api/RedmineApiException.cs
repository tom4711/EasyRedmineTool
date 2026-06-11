namespace EasyRedmineTool.Core.Api;

public sealed class RedmineApiException : Exception
{
    public RedmineApiException(string endpoint, int statusCode, string? reasonPhrase, string? responseBody = null)
        : base(FormatMessage(endpoint, statusCode, reasonPhrase, responseBody))
    {
        Endpoint = endpoint;
        StatusCode = statusCode;
    }

    public string Endpoint { get; }

    public int StatusCode { get; }

    private static string FormatMessage(string endpoint, int statusCode, string? reasonPhrase, string? responseBody)
    {
        var summary = $"Redmine-Anfrage fehlgeschlagen ({statusCode}";
        if (!string.IsNullOrWhiteSpace(reasonPhrase))
        {
            summary += $" {reasonPhrase.Trim()}";
        }

        summary += $"): {endpoint}";

        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return summary;
        }

        var snippet = responseBody.Trim();
        if (snippet.Length > 200)
        {
            snippet = snippet[..200] + "…";
        }

        return $"{summary} — {snippet}";
    }
}
