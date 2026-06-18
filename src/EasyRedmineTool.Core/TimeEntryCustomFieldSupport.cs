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
        var fields = new Dictionary<int, (string Name, List<string> Values, bool IsMultiple)>();

        foreach (var definition in applicableDefinitions)
        {
            fields[definition.Id] = (definition.Name, [], definition.Multiple);
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
            .Select(field => CreateRow(
                field.Key,
                field.Value.Name,
                field.Value.Values,
                field.Value.IsMultiple,
                fieldMeta.GetValueOrDefault(field.Key)))
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
            if (!row.IsRequired)
            {
                continue;
            }

            if (row.IsMultiple)
            {
                if (row.SelectedValues.Count == 0)
                {
                    return $"Bitte „{row.Name}“ ausfüllen.";
                }

                continue;
            }

            if (string.IsNullOrWhiteSpace(row.Value))
            {
                return $"Bitte „{row.Name}“ ausfüllen.";
            }
        }

        return null;
    }

    public static IReadOnlyList<TimeEntryCustomFieldValue> BuildValues(IEnumerable<TimeEntryCustomFieldRowViewModel> rows) =>
        rows
            .Where(row => row.Id > 0)
            .Select(BuildValue)
            .Where(value => value is not null)
            .Cast<TimeEntryCustomFieldValue>()
            .ToList();

    public static TimeEntryCustomFieldRowViewModel CreateProbedRow(
        int id,
        string name,
        string value = "",
        bool isMultiple = false) =>
        new(
            id,
            name,
            isRequired: true,
            hasPossibleValues: id > 0,
            isSearchableList: false,
            isMultiple: isMultiple,
            possibleValues: [],
            selectedValues: string.IsNullOrWhiteSpace(value) ? [] : [value]);

    public static string FormatMissingFieldPrompt(string fieldName, int fieldId) =>
        fieldId > 0
            ? $"Bitte „{fieldName}“ ausfüllen und erneut buchen."
            : $"Bitte „{fieldName}“ ausfüllen und erneut buchen. (Feld-ID konnte nicht automatisch ermittelt werden – ggf. in settings.json unter TimeEntryCustomFieldIdMappings eintragen.)";

    public static void SaveDefaults(IAppSettingsService settingsService, IEnumerable<TimeEntryCustomFieldRowViewModel> rows)
    {
        var defaults = rows
            .Select(row => new TimeEntryCustomFieldDefault
            {
                Id = row.Id,
                Name = row.Name,
                Value = row.IsMultiple
                    ? string.Empty
                    : row.Value.Trim(),
                Values = row.IsMultiple
                    ? row.SelectedValues.ToList()
                    : string.IsNullOrWhiteSpace(row.Value) ? [] : [row.Value.Trim()]
            })
            .Where(row => row.Values.Count > 0 || !string.IsNullOrWhiteSpace(row.Value))
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

    private static TimeEntryCustomFieldValue? BuildValue(TimeEntryCustomFieldRowViewModel row)
    {
        if (row.IsMultiple)
        {
            if (row.SelectedValues.Count == 0)
            {
                return null;
            }

            return new TimeEntryCustomFieldValue
            {
                Id = row.Id,
                IsMultiple = true,
                Values = row.SelectedValues.ToList()
            };
        }

        if (string.IsNullOrWhiteSpace(row.Value))
        {
            return null;
        }

        return new TimeEntryCustomFieldValue
        {
            Id = row.Id,
            Value = row.Value.Trim()
        };
    }

    private static void ApplyKnownValues(
        Dictionary<int, (string Name, List<string> Values, bool IsMultiple)> fields,
        IEnumerable<TimeEntryCustomFieldDefault> defaults,
        IReadOnlySet<int> allowedIds)
    {
        foreach (var field in defaults.Where(field => allowedIds.Contains(field.Id)))
        {
            UpsertField(fields, field.Id, field.Name, ResolveDefaultValues(field), field.Values.Count > 1);
        }
    }

    private static void ApplyKnownValues(
        Dictionary<int, (string Name, List<string> Values, bool IsMultiple)> fields,
        IEnumerable<TimeEntryCustomFieldValueDto> values,
        IReadOnlySet<int> allowedIds)
    {
        foreach (var value in values.Where(value => allowedIds.Contains(value.Id)))
        {
            UpsertField(
                fields,
                value.Id,
                value.Name,
                value.GetEffectiveValues().ToList(),
                value.Multiple || value.GetEffectiveValues().Count > 1);
        }
    }

    private static TimeEntryCustomFieldRowViewModel CreateRow(
        int id,
        string name,
        IReadOnlyList<string> selectedValues,
        bool isMultiple,
        TimeEntryCustomFieldDefinitionDto? definition) =>
        new(
            id,
            name,
            isRequired: definition?.IsRequired ?? false,
            hasPossibleValues: definition?.HasPossibleValues ?? false,
            isSearchableList: definition?.IsSearchableList ?? false,
            isMultiple: isMultiple || definition?.Multiple == true,
            possibleValues: definition?.PossibleValues ?? [],
            selectedValues: selectedValues);

    private static IReadOnlyList<string> ResolveDefaultValues(TimeEntryCustomFieldDefault field) =>
        field.Values.Count > 0
            ? field.Values
            : string.IsNullOrWhiteSpace(field.Value) ? [] : [field.Value];

    private static void UpsertField(
        Dictionary<int, (string Name, List<string> Values, bool IsMultiple)> fields,
        int id,
        string name,
        IReadOnlyList<string> values,
        bool isMultiple)
    {
        if (!fields.TryGetValue(id, out var current))
        {
            fields[id] = (name, values.ToList(), isMultiple);
            return;
        }

        var resolvedName = string.IsNullOrWhiteSpace(name) ? current.Name : name;
        if (values.Count > 0)
        {
            fields[id] = (resolvedName, values.ToList(), isMultiple || current.IsMultiple);
            return;
        }

        fields[id] = (resolvedName, current.Values, current.IsMultiple || isMultiple);
    }
}
