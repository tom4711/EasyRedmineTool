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
        IReadOnlyList<TimeEntryCustomFieldValueDto>? existingValues = null)
    {
        if (definitions.Count == 0 &&
            settings.TimeEntryCustomFieldDefaults.Count == 0 &&
            recentValues.Count == 0 &&
            existingValues is not { Count: > 0 })
        {
            return [];
        }

        var savedValues = settings.TimeEntryCustomFieldDefaults.ToDictionary(
            field => field.Id,
            field => field.Value);

        foreach (var recentValue in recentValues)
        {
            if (!string.IsNullOrWhiteSpace(recentValue.Value))
            {
                savedValues[recentValue.Id] = recentValue.Value;
            }
        }

        if (existingValues is not null)
        {
            foreach (var existingValue in existingValues)
            {
                if (!string.IsNullOrWhiteSpace(existingValue.Value))
                {
                    savedValues[existingValue.Id] = existingValue.Value;
                }
            }
        }

        if (definitions.Count > 0)
        {
            return definitions
                .OrderBy(field => field.Name)
                .Select(field => new TimeEntryCustomFieldRowViewModel(
                    field.Id,
                    field.Name,
                    field.Is_Required,
                    field.HasPossibleValues,
                    field.Possible_Values,
                    savedValues.GetValueOrDefault(field.Id) ?? field.Default_Value ?? string.Empty))
                .ToList();
        }

        if (settings.TimeEntryCustomFieldDefaults.Count > 0)
        {
            return settings.TimeEntryCustomFieldDefaults
                .OrderBy(field => field.Name)
                .Select(field => new TimeEntryCustomFieldRowViewModel(
                    field.Id,
                    field.Name,
                    isRequired: true,
                    hasPossibleValues: false,
                    possibleValues: [],
                    value: field.Value))
                .ToList();
        }

        return recentValues
            .OrderBy(field => field.Name)
            .Select(field => new TimeEntryCustomFieldRowViewModel(
                field.Id,
                field.Name,
                isRequired: true,
                hasPossibleValues: false,
                possibleValues: [],
                value: field.Value ?? string.Empty))
            .ToList();
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
}
