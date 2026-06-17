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
        IReadOnlyList<TimeEntryCustomFieldValueDto>? existingValues = null)
    {
        var fieldMeta = definitions
            .Where(definition => AppliesToProject(definition, projectId))
            .ToDictionary(definition => definition.Id);

        var fields = new Dictionary<int, (string Name, string Value)>();

        foreach (var definition in fieldMeta.Values)
        {
            fields[definition.Id] = (definition.Name, string.Empty);
        }

        foreach (var field in settings.TimeEntryCustomFieldDefaults)
        {
            fields[field.Id] = (field.Name, field.Value);
        }

        foreach (var recentValue in recentValues)
        {
            UpsertField(fields, recentValue);
        }

        if (existingValues is not null)
        {
            foreach (var existingValue in existingValues)
            {
                UpsertField(fields, existingValue);
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
            .Where(row => !string.IsNullOrWhiteSpace(row.Value))
            .Select(row => new TimeEntryCustomFieldValue
            {
                Id = row.Id,
                Value = row.Value.Trim()
            })
            .ToList();

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

        settingsService.Update(settings => settings.TimeEntryCustomFieldDefaults = defaults);
    }

    private static TimeEntryCustomFieldRowViewModel CreateRow(
        int id,
        string name,
        string value,
        TimeEntryCustomFieldDefinitionDto? definition) =>
        new(
            id,
            name,
            isRequired: definition?.IsRequired ?? true,
            hasPossibleValues: definition?.HasPossibleValues ?? false,
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
