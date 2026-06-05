namespace EasyRedmineTool.Core.Api;

using System.Text.Json;

internal static class RedmineJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
