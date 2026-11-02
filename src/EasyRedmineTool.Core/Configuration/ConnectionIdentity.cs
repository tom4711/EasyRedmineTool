namespace EasyRedmineTool.Core.Configuration;

using System.Security.Cryptography;
using System.Text;

/// <summary>Offline connection scope. A rotated key deliberately creates a new scope.</summary>
public static class ConnectionIdentity
{
    public static string Get(string baseUrl, string apiKey)
    {
        var normalized = (baseUrl ?? string.Empty).Trim().TrimEnd('/');
        if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
        {
            var builder = new UriBuilder(uri) { Host = uri.Host.ToLowerInvariant(), Scheme = uri.Scheme.ToLowerInvariant() };
            if (uri.IsDefaultPort) builder.Port = -1;
            normalized = builder.Uri.AbsoluteUri.TrimEnd('/');
        }

        // Length-prefixing avoids ambiguous URL/key concatenations; exact key bytes matter.
        var input = normalized.Length + ":" + normalized + (apiKey ?? string.Empty);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
    }
}
