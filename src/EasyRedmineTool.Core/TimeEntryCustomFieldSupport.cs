namespace EasyRedmineTool.Core;

using EasyRedmineTool.Core.Configuration;
using EasyRedmineTool.Core.Models.TimeEntries;
using EasyRedmineTool.Core.Services.Interfaces;
using EasyRedmineTool.Core.ViewModels;

public static class TimeEntryCustomFieldSupport
{
    public static IReadOnlyList<TimeEntryCustomFieldRowViewModel> CreateRows(
        IReadOnlyList<TimeEntryCustomFieldDefinitionDto> definitions,
        IReadOnlyList<TimeEntryCustomFieldValueDto> recentValues,
        AppSettings settings,
        int? projectId = null,
        int? activityId = null,
        IReadOnlyList<TimeEntryCustomFieldValueDto>? existingValues = null)
    {
        var applicableDefinitions = definitions
            .Where(definition => AppliesToProject(definition, projectId))
            .Where(definition => AppliesToActivity(definition, activityId))
            .ToList();

        var fieldMeta = applicableDefinitions.ToDictionary(definition => definition.Id);
        var allowedIds = fieldMeta.Keys.ToHashSet();
        var fields = new Dictionary<int, (string Name, string Value)>();

        foreach (var definition in applicableDefinitions)
        {
            fields[definition.Id] = (definition.Name, string.Empty);
        }

        if (allowedIds.Count > 0)
        {
            ApplyKnownValues(fields, settings.TimeEntryCustomFieldDefaults, allowedIds);
            ApplyKnownValues(fields, recentValues, allowedIds);
            if (existingValues is not null)
            {
                ApplyKnownValues(fields, existingValues, allowedIds);
            }
        }

        if (fields.Count == 0)
        {
            return [];
        }

        return fields
            .OrderBy(field => field.Value.Name)
            .Select(field => CreateRow(field.Key, field.Value.Name, field.Value.Value, fieldMeta.GetValueOrDefault(field.Key)))
            .ToList();
    }

    public static bool AppliesToProject(TimeEntryCustomFieldDefinitionDto definition, int? projectId)
    {
        if (definition.IsProjectScoped)
        {
            return true;
        }

        if (!projectId.HasValue)
        {
            return true;
        }

        if (definition.IsForAll)
        {
            return true;
        }

        if (definition.ProjectIds.Count == 0)
        {
            return false;
        }

        return definition.ProjectIds.Contains(projectId.Value);
    }

    public static bool AppliesToActivity(TimeEntryCustomFieldDefinitionDto definition, int? activityId)
    {
        if (definition.ActivityIds.Count == 0)
        {
            return true;
        }

        if (!activityId.HasValue)
        {
            return false;
        }

        return definition.ActivityIds.Contains(activityId.Value);
    }

    public static string? Validate(IEnumerable<TimeEntryCustomFieldRowViewModel> rows)
    {
        foreach (var row in rows)
        {
            if (row.IsRequired && string.IsNullOrWhiteSpace(row.Value))
            {
                return $"Bitte „{row.Name}“ ausfüllen.";
            }
        }

        return null;
    }

    public static IReadOnlyList<TimeEntryCustomFieldValue> BuildValues(IEnumerable<TimeEntryCustomFieldRowViewModel> rows) =>
        rows
            .Where(row => row.Id > 0 && !string.IsNullOrWhiteSpace(row.Value))
            .Select(row => new TimeEntryCustomFieldValue
            {
                Id = row.Id,
                Value = row.Value.Trim()
            })
            .ToList();

    public static TimeEntryCustomFieldRowViewModel CreateProbedRow(
        int id,
        string name,
        string value = "") =>
        new(
            id,
            name,
            isRequired: true,
            hasPossibleValues: id > 0,
            isSearchableList: false,
            possibleValues: [],
            value: value);

    public static string FormatMissingFieldPrompt(string fieldName, int fieldId) =>
        fieldId > 0
            ? $"Bitte „{fieldName}“ ausfüllen und erneut buchen."
            : $"Bitte „{fieldName}“ ausfüllen und erneut buchen. (Feld-ID konnte nicht automatisch ermittelt werden – ggf. in settings.json unter TimeEntryCustomFieldIdMappings eintragen.)";

    public static void SaveDefaults(IAppSettingsService settingsService, IEnumerable<TimeEntryCustomFieldRowViewModel> rows)
    {
        var defaults = rows
            .Where(row => !string.IsNullOrWhiteSpace(row.Value))
            .Select(row => new TimeEntryCustomFieldDefault
            {
                Id = row.Id,
                Name = row.Name,
                Value = row.Value.Trim()
            })
            .ToList();

        if (defaults.Count == 0)
        {
            return;
        }

        settingsService.Update(settings =>
        {
            var merged = settings.TimeEntryCustomFieldDefaults
                .Where(existing => defaults.All(row => row.Id != existing.Id))
                .Concat(defaults)
                .ToList();
            settings.TimeEntryCustomFieldDefaults = merged;
        });
    }

    private static void ApplyKnownValues(
        Dictionary<int, (string Name, string Value)> fields,
        IEnumerable<TimeEntryCustomFieldDefault> defaults,
        IReadOnlySet<int> allowedIds)
    {
        foreach (var field in defaults.Where(field => allowedIds.Contains(field.Id)))
        {
            UpsertField(fields, new TimeEntryCustomFieldValueDto
            {
                Id = field.Id,
                Name = field.Name,
                Value = field.Value
            });
        }
    }

    private static void ApplyKnownValues(
        Dictionary<int, (string Name, string Value)> fields,
        IEnumerable<TimeEntryCustomFieldValueDto> values,
        IReadOnlySet<int> allowedIds)
    {
        foreach (var value in values.Where(value => allowedIds.Contains(value.Id)))
        {
            UpsertField(fields, value);
        }
    }

    private static TimeEntryCustomFieldRowViewModel CreateRow(
        int id,
        string name,
        string value,
        TimeEntryCustomFieldDefinitionDto? definition) =>
        new(
            id,
            name,
            isRequired: definition?.IsRequired ?? false,
            hasPossibleValues: definition?.HasPossibleValues ?? false,
            isSearchableList: definition?.IsSearchableList ?? false,
            possibleValues: definition?.PossibleValues ?? [],
            value: value);

    private static void UpsertField(
        Dictionary<int, (string Name, string Value)> fields,
        TimeEntryCustomFieldValueDto value)
    {
        if (!fields.TryGetValue(value.Id, out var current))
        {
            fields[value.Id] = (value.Name, value.Value ?? string.Empty);
            return;
        }

        var name = string.IsNullOrWhiteSpace(value.Name) ? current.Name : value.Name;
        if (!string.IsNullOrWhiteSpace(value.Value))
        {
            fields[value.Id] = (name, value.Value);
            return;
        }

        fields[value.Id] = (name, current.Value);
    }
}
