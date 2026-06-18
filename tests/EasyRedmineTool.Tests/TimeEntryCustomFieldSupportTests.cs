namespace EasyRedmineTool.Tests;

using EasyRedmineTool.Core;
using EasyRedmineTool.Core.Configuration;
using EasyRedmineTool.Core.Models.TimeEntries;
using EasyRedmineTool.Core.ViewModels;

using System.Text.Json;

public class TimeEntryCustomFieldSupportTests
{
    private static TimeEntryCustomFieldDefinitionDto CreateDefinition(
        int id,
        string name,
        bool isRequired = false,
        IReadOnlyList<int>? projectIds = null,
        IReadOnlyList<int>? activityIds = null,
        IReadOnlyList<string>? possibleValues = null) =>
        new()
        {
            Id = id,
            Name = name,
            FieldFormat = possibleValues is { Count: > 0 } ? "list" : "string",
            IsRequired = isRequired,
            IsForAll = false,
            ProjectIds = projectIds ?? [],
            ActivityIds = activityIds ?? [],
            PossibleValues = possibleValues ?? []
        };

    [Fact]
    public void Validate_returns_message_for_missing_required_field()
    {
        var rows = TimeEntryCustomFieldSupport.CreateRows(
            [CreateDefinition(5, CustomFieldTestData.ListFieldName, isRequired: true, projectIds: [42])],
            [],
            new AppSettings(),
            projectId: 42,
            activityId: 10);

        var message = TimeEntryCustomFieldSupport.Validate(rows);

        Assert.Contains(CustomFieldTestData.ListFieldName, message, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateRows_prefers_existing_entry_values_over_defaults()
    {
        var rows = TimeEntryCustomFieldSupport.CreateRows(
            [CreateDefinition(5, CustomFieldTestData.ListFieldName, projectIds: [42])],
            [],
            new AppSettings
            {
                TimeEntryCustomFieldDefaults =
                [
                    new TimeEntryCustomFieldDefault { Id = 5, Name = CustomFieldTestData.ListFieldName, Value = "A" }
                ]
            },
            projectId: 42,
            activityId: 10,
            existingValues:
            [
                new TimeEntryCustomFieldValueDto { Id = 5, Name = CustomFieldTestData.ListFieldName, Value = "B" }
            ]);

        Assert.Single(rows);
        Assert.Equal("B", rows[0].Value);
    }

    [Fact]
    public void CreateRows_ignores_recent_values_without_matching_definition()
    {
        var rows = TimeEntryCustomFieldSupport.CreateRows(
            [],
            [
                new TimeEntryCustomFieldValueDto
                {
                    Id = 5,
                    Name = CustomFieldTestData.ListFieldName,
                    Value = "Value A"
                }
            ],
            new AppSettings(),
            projectId: 42,
            activityId: 10);

        Assert.Empty(rows);
    }

    [Fact]
    public void CreateRows_uses_project_definitions_even_without_recent_values()
    {
        var rows = TimeEntryCustomFieldSupport.CreateRows(
            [
                new TimeEntryCustomFieldDefinitionDto
                {
                    Id = 7,
                    Name = "Cost center",
                    FieldFormat = "list",
                    IsRequired = true,
                    IsForAll = false,
                    ProjectIds = [42],
                    PossibleValues = ["A", "B"]
                }
            ],
            [],
            new AppSettings(),
            projectId: 42);

        Assert.Single(rows);
        Assert.Equal(7, rows[0].Id);
        Assert.True(rows[0].IsRequired);
        Assert.True(rows[0].HasPossibleValues);
        Assert.Equal(["A", "B"], rows[0].PossibleValues);
    }

    [Fact]
    public void CreateRows_filters_definitions_by_project()
    {
        var rows = TimeEntryCustomFieldSupport.CreateRows(
            [
                new TimeEntryCustomFieldDefinitionDto
                {
                    Id = 1,
                    Name = "Project A field",
                    ProjectIds = [10]
                },
                new TimeEntryCustomFieldDefinitionDto
                {
                    Id = 2,
                    Name = "Project B field",
                    ProjectIds = [20]
                }
            ],
            [],
            new AppSettings(),
            projectId: 10);

        Assert.Single(rows);
        Assert.Equal(1, rows[0].Id);
    }

    [Fact]
    public void CreateRows_filters_definitions_by_activity()
    {
        var rows = TimeEntryCustomFieldSupport.CreateRows(
            [
                new TimeEntryCustomFieldDefinitionDto
                {
                    Id = 5,
                    Name = CustomFieldTestData.ListFieldName,
                    IsProjectScoped = true,
                    ActivityIds = [100]
                },
                new TimeEntryCustomFieldDefinitionDto
                {
                    Id = 6,
                    Name = "Other field",
                    IsProjectScoped = true,
                    ActivityIds = [200]
                }
            ],
            [],
            new AppSettings(),
            projectId: 42,
            activityId: 100);

        Assert.Single(rows);
        Assert.Equal(5, rows[0].Id);
    }

    [Fact]
    public void AppliesToProject_includes_global_fields()
    {
        var definition = new TimeEntryCustomFieldDefinitionDto
        {
            IsForAll = true,
            ProjectIds = [99]
        };

        Assert.True(TimeEntryCustomFieldSupport.AppliesToProject(definition, 42));
    }

    [Fact]
    public void AppliesToProject_includes_project_scoped_fields()
    {
        var definition = new TimeEntryCustomFieldDefinitionDto
        {
            IsProjectScoped = true
        };

        Assert.True(TimeEntryCustomFieldSupport.AppliesToProject(definition, 42));
    }

    [Fact]
    public void BuildValues_includes_only_filled_fields()
    {
        var rows = TimeEntryCustomFieldSupport.CreateRows(
            [
                CreateDefinition(1, "A", projectIds: [42]),
                CreateDefinition(2, "B", projectIds: [42])
            ],
            [],
            new AppSettings(),
            projectId: 42,
            existingValues:
            [
                new TimeEntryCustomFieldValueDto { Id = 1, Name = "A", Value = "x" },
                new TimeEntryCustomFieldValueDto { Id = 2, Name = "B", Value = string.Empty }
            ]);

        var values = TimeEntryCustomFieldSupport.BuildValues(rows);

        Assert.Single(values);
        Assert.Equal(1, values[0].Id);
        Assert.Equal("x", values[0].Value);
    }

    [Fact]
    public void Searchable_row_filters_possible_values_by_contains()
    {
        var row = new TimeEntryCustomFieldRowViewModel(
            id: 12,
            name: CustomFieldTestData.ListFieldName,
            isRequired: true,
            hasPossibleValues: true,
            isSearchableList: true,
            isMultiple: true,
            possibleValues: CustomFieldTestData.ThreeOptions);

        row.SearchText = "Alpha";

        Assert.Equal([CustomFieldTestData.OptionAlpha], row.FilteredPossibleValues);
    }

    [Fact]
    public void BuildValues_serializes_multiple_selection_as_array()
    {
        var row = new TimeEntryCustomFieldRowViewModel(
            id: 12,
            name: CustomFieldTestData.ListFieldName,
            isRequired: true,
            hasPossibleValues: true,
            isSearchableList: true,
            isMultiple: true,
            possibleValues: CustomFieldTestData.TwoOptions,
            selectedValues: CustomFieldTestData.TwoOptions);

        var values = TimeEntryCustomFieldSupport.BuildValues([row]);

        Assert.Single(values);
        Assert.True(values[0].IsMultiple);
        Assert.Equal(CustomFieldTestData.TwoOptions, values[0].Values);
    }

    [Fact]
    public void CreateRows_loads_multiple_values_from_existing_entry()
    {
        var rows = TimeEntryCustomFieldSupport.CreateRows(
            [
                new TimeEntryCustomFieldDefinitionDto
                {
                    Id = 12,
                    Name = CustomFieldTestData.ListFieldName,
                    FieldFormat = "list",
                    Multiple = true,
                    IsProjectScoped = true,
                    PossibleValues = CustomFieldTestData.TwoOptions
                }
            ],
            [],
            new AppSettings(),
            projectId: 42,
            activityId: CustomFieldTestData.ActivityId,
            existingValues:
            [
                new TimeEntryCustomFieldValueDto
                {
                    Id = 12,
                    Name = CustomFieldTestData.ListFieldName,
                    Multiple = true,
                    SelectedValues = CustomFieldTestData.TwoOptions.ToList()
                }
            ]);

        Assert.Single(rows);
        Assert.True(rows[0].IsMultiple);
        Assert.Equal(2, rows[0].SelectedValues.Count);
    }

    [Fact]
    public void Deserialize_custom_field_value_supports_string_or_array()
    {
        var single = JsonSerializer.Deserialize<TimeEntryCustomFieldValueDto>(
            $$"""{"id":12,"value":"{{CustomFieldTestData.OptionAlpha}}"}""",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var multiple = JsonSerializer.Deserialize<TimeEntryCustomFieldValueDto>(
            $$"""{"id":12,"multiple":true,"value":["{{CustomFieldTestData.OptionAlpha}}","{{CustomFieldTestData.OptionBeta}}"]}""",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.Equal([CustomFieldTestData.OptionAlpha], single!.SelectedValues);
        Assert.Equal(CustomFieldTestData.TwoOptions, multiple!.SelectedValues);
    }
}
