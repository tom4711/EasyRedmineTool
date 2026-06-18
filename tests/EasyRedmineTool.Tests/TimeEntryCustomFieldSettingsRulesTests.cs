namespace EasyRedmineTool.Tests;

using EasyRedmineTool.Core;
using EasyRedmineTool.Core.Configuration;

public class TimeEntryCustomFieldSettingsRulesTests
{
    [Fact]
    public void Resolve_returns_configured_field_for_matching_activity_name()
    {
        var settings = new AppSettings
        {
            TimeEntryCustomFieldActivityRules =
            [
                new TimeEntryCustomFieldActivityRule
                {
                    ActivityName = CustomFieldTestData.ActivityName,
                    Fields =
                    [
                        new TimeEntryCustomFieldActivityRuleField
                        {
                            Name = CustomFieldTestData.ListFieldName,
                            Id = 77,
                            DefaultValue = CustomFieldTestData.OptionAlpha,
                            IsRequired = true,
                            IsMultiple = true,
                            PossibleValues = CustomFieldTestData.TwoOptions.ToList()
                        }
                    ]
                }
            ]
        };

        var result = TimeEntryCustomFieldSettingsRules.Resolve(
            settings,
            activityId: CustomFieldTestData.ActivityId,
            activityName: CustomFieldTestData.ActivityName);

        Assert.Single(result.Definitions);
        Assert.Equal(77, result.Definitions[0].Id);
        Assert.Equal(CustomFieldTestData.ListFieldName, result.Definitions[0].Name);
        Assert.True(result.Definitions[0].IsRequired);
        Assert.True(result.Definitions[0].Multiple);
        Assert.True(result.Definitions[0].HasPossibleValues);
        Assert.False(result.Definitions[0].IsSearchableList);
        Assert.Equal(2, result.Definitions[0].PossibleValues.Count);
        Assert.Empty(result.DefaultValues);
    }

    [Fact]
    public void Resolve_matches_activity_name_case_insensitively()
    {
        var settings = new AppSettings
        {
            TimeEntryCustomFieldActivityRules =
            [
                new TimeEntryCustomFieldActivityRule
                {
                    ActivityName = CustomFieldTestData.ActivityName,
                    Fields =
                    [
                        new TimeEntryCustomFieldActivityRuleField
                        {
                            Name = CustomFieldTestData.ListFieldName,
                            Id = CustomFieldTestData.ListFieldId
                        }
                    ]
                }
            ]
        };

        var result = TimeEntryCustomFieldSettingsRules.Resolve(
            settings,
            activityId: 1,
            activityName: "product support");

        Assert.Single(result.Definitions);
    }

    [Fact]
    public void Resolve_requires_matching_activity_id_when_configured()
    {
        var settings = new AppSettings
        {
            TimeEntryCustomFieldActivityRules =
            [
                new TimeEntryCustomFieldActivityRule
                {
                    ActivityName = CustomFieldTestData.ActivityName,
                    ActivityId = CustomFieldTestData.ActivityId,
                    Fields =
                    [
                        new TimeEntryCustomFieldActivityRuleField
                        {
                            Name = CustomFieldTestData.ListFieldName,
                            Id = CustomFieldTestData.ListFieldId
                        }
                    ]
                }
            ]
        };

        Assert.Empty(TimeEntryCustomFieldSettingsRules.Resolve(
            settings,
            CustomFieldTestData.OtherActivityId,
            CustomFieldTestData.ActivityName).Definitions);
        Assert.Single(TimeEntryCustomFieldSettingsRules.Resolve(
            settings,
            CustomFieldTestData.ActivityId,
            CustomFieldTestData.ActivityName).Definitions);
    }

    [Fact]
    public void LoadPossibleValues_reads_values_from_external_json_file()
    {
        var filePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(filePath, $$"""["{{CustomFieldTestData.OptionAlpha}}", "{{CustomFieldTestData.OptionBeta}}"]""");

        try
        {
            var values = TimeEntryCustomFieldSettingsRules.LoadPossibleValues(
                new TimeEntryCustomFieldActivityRuleField
                {
                    PossibleValuesFile = filePath
                },
                settingsDirectory: null);

            Assert.Equal(CustomFieldTestData.TwoOptions, values);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void ResolveFieldId_finds_id_from_activity_rules()
    {
        var settings = new AppSettings
        {
            TimeEntryCustomFieldActivityRules =
            [
                new TimeEntryCustomFieldActivityRule
                {
                    ActivityName = CustomFieldTestData.ActivityName,
                    Fields =
                    [
                        new TimeEntryCustomFieldActivityRuleField
                        {
                            Name = CustomFieldTestData.ListFieldName,
                            Id = 88
                        }
                    ]
                }
            ]
        };

        Assert.Equal(88, TimeEntryCustomFieldSettingsRules.ResolveFieldId(settings, CustomFieldTestData.ListFieldName));
    }
}
