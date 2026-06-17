namespace EasyRedmineTool.Core;

using System.Text.Json;
using System.Text.RegularExpressions;

public static partial class TimeEntryCustomFieldProbe
{
    [GeneratedRegex(@"^(.+?) darf nicht leer sein\.?$", RegexOptions.IgnoreCase)]
    private static partial Regex RequiredFieldGermanRegex();

    [GeneratedRegex(@"^(.+?) cannot be blank\.?$", RegexOptions.IgnoreCase)]
    private static partial Regex RequiredFieldEnglishRegex();

    [GeneratedRegex(@"^(.+?) muss ausgefüllt werden\.?$", RegexOptions.IgnoreCase)]
    private static partial Regex RequiredFieldGermanAltRegex();

    [GeneratedRegex(@"custom field\s+#?(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex CustomFieldIdEnglishRegex();

    [GeneratedRegex(@"#(\d+)")]
    private static partial Regex HashIdRegex();

    public static IReadOnlyList<string> ParseRequiredFieldNamesFromMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return [];
        }

        var jsonStart = message.IndexOf('{', StringComparison.Ordinal);
        if (jsonStart >= 0)
        {
            var fromJson = ParseRequiredFieldNames(message[jsonStart..]);
            if (fromJson.Count > 0)
            {
                return fromJson;
            }
        }

        return ParseRequiredFieldNames(message);
    }

    public static int? TryParseFieldIdFromError(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var englishMatch = CustomFieldIdEnglishRegex().Match(message);
        if (englishMatch.Success && int.TryParse(englishMatch.Groups[1].Value, out var englishId))
        {
            return englishId;
        }

        var hashMatch = HashIdRegex().Match(message);
        if (hashMatch.Success && int.TryParse(hashMatch.Groups[1].Value, out var hashId))
        {
            return hashId;
        }

        return null;
    }

    public static IReadOnlyList<string> ParseRequiredFieldNames(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return [];
        }

        var names = new List<string>();

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            if (document.RootElement.TryGetProperty("errors", out var errors)
                && errors.ValueKind == JsonValueKind.Array)
            {
                foreach (var error in errors.EnumerateArray())
                {
                    if (error.ValueKind == JsonValueKind.String)
                    {
                        AddRequiredName(names, error.GetString());
                    }
                }
            }
        }
        catch (JsonException)
        {
            AddRequiredName(names, responseBody);
        }

        return names
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AddRequiredName(List<string> names, string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        foreach (var regex in new[] { RequiredFieldGermanRegex(), RequiredFieldGermanAltRegex(), RequiredFieldEnglishRegex() })
        {
            var match = regex.Match(message.Trim());
            if (match.Success)
            {
                names.Add(match.Groups[1].Value.Trim());
                return;
            }
        }
    }
}
