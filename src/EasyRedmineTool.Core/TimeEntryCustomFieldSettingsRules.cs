namespace EasyRedmineTool.Core;

using EasyRedmineTool.Core.Configuration;
using EasyRedmineTool.Core.Models.TimeEntries;

using System.Text.Json;

public static class TimeEntryCustomFieldSettingsRules
{
    private static readonly JsonDocumentOptions JsonDocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip
    };

    public static TimeEntryCustomFieldRuleResolution Resolve(
        AppSettings settings,
        int? activityId,
        string? activityName,
        string? settingsDirectory = null)
    {
        var matchingRules = settings.TimeEntryCustomFieldActivityRules
            .Where(rule => MatchesActivity(rule, activityId, activityName))
            .ToList();

        if (matchingRules.Count == 0)
        {
            return TimeEntryCustomFieldRuleResolution.Empty;
        }

        var definitions = new List<TimeEntryCustomFieldDefinitionDto>();

        foreach (var field in matchingRules.SelectMany(rule => rule.Fields))
        {
            if (string.IsNullOrWhiteSpace(field.Name) || field.Id <= 0)
            {
                continue;
            }

            var possibleValues = LoadPossibleValues(field, settingsDirectory);
            definitions.Add(new TimeEntryCustomFieldDefinitionDto
            {
                Id = field.Id,
                Name = field.Name.Trim(),
                FieldFormat = possibleValues.Count > 0 ? "depending_enumeration" : "string",
                IsRequired = field.IsRequired,
                IsProjectScoped = true,
                Multiple = field.IsMultiple,
                ActivityIds = activityId.HasValue ? [activityId.Value] : [],
                PossibleValues = possibleValues
            });
        }

        return new TimeEntryCustomFieldRuleResolution(definitions, []);
    }

    public static IReadOnlyList<string> LoadPossibleValues(
        TimeEntryCustomFieldActivityRuleField field,
        string? settingsDirectory)
    {
        var inlineValues = field.PossibleValues
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (inlineValues.Count > 0)
        {
            return inlineValues;
        }

        if (string.IsNullOrWhiteSpace(field.PossibleValuesFile))
        {
            return [];
        }

        var filePath = Path.IsPathRooted(field.PossibleValuesFile)
            ? field.PossibleValuesFile
            : Path.Combine(settingsDirectory ?? string.Empty, field.PossibleValuesFile);

        if (!File.Exists(filePath))
        {
            return [];
        }

        try
        {
            var json = File.ReadAllText(filePath);
            return ParsePossibleValuesJson(json);
        }
        catch
        {
            return [];
        }
    }

    internal static IReadOnlyList<string> ParsePossibleValuesJson(string json)
    {
        using var document = JsonDocument.Parse(json, JsonDocumentOptions);
        var root = document.RootElement;

        if (root.ValueKind == JsonValueKind.Array)
        {
            return ParseStringArray(root);
        }

        foreach (var propertyName in new[] { "values", "possibleValues", "PossibleValues" })
        {
            if (root.TryGetProperty(propertyName, out var array) && array.ValueKind == JsonValueKind.Array)
            {
                return ParseStringArray(array);
            }
        }

        return [];
    }

    private static List<string> ParseStringArray(JsonElement array) =>
        array.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static int? ResolveFieldId(AppSettings settings, string fieldName) =>
        settings.TimeEntryCustomFieldActivityRules
            .SelectMany(rule => rule.Fields)
            .FirstOrDefault(field => field.Id > 0
                && string.Equals(field.Name, fieldName, StringComparison.OrdinalIgnoreCase))
            ?.Id;

    public static bool MatchesActivity(
        TimeEntryCustomFieldActivityRule rule,
        int? activityId,
        string? activityName)
    {
        if (rule.ActivityId.HasValue)
        {
            if (!activityId.HasValue || rule.ActivityId.Value != activityId.Value)
            {
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(rule.ActivityName))
        {
            return rule.ActivityId.HasValue;
        }

        if (string.IsNullOrWhiteSpace(activityName))
        {
            return false;
        }

        return string.Equals(rule.ActivityName.Trim(), activityName.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public sealed class TimeEntryCustomFieldRuleResolution
    {
        public static TimeEntryCustomFieldRuleResolution Empty { get; } = new([], []);

        public TimeEntryCustomFieldRuleResolution(
            IReadOnlyList<TimeEntryCustomFieldDefinitionDto> definitions,
            IReadOnlyList<TimeEntryCustomFieldValueDto> defaultValues)
        {
            Definitions = definitions;
            DefaultValues = defaultValues;
        }

        public IReadOnlyList<TimeEntryCustomFieldDefinitionDto> Definitions { get; }

        public IReadOnlyList<TimeEntryCustomFieldValueDto> DefaultValues { get; }
    }
}