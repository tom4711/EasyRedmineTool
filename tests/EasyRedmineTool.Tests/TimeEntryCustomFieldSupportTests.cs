namespace EasyRedmineTool.Tests;

using EasyRedmineTool.Core;
using EasyRedmineTool.Core.Configuration;
using EasyRedmineTool.Core.Models.TimeEntries;

public class TimeEntryCustomFieldSupportTests
{
    [Fact]
    public void Validate_returns_message_for_missing_required_field()
    {
        var rows = TimeEntryCustomFieldSupport.CreateRows(
            [
                new TimeEntryCustomFieldDefinitionDto
                {
                    Id = 5,
                    Name = "Produktdaten Hierarchie",
                    Is_Required = true,
                    Field_Format = "list",
                    Possible_Values = ["A", "B"]
                }
            ],
            [],
            new AppSettings());

        var message = TimeEntryCustomFieldSupport.Validate(rows);

        Assert.Contains("Produktdaten Hierarchie", message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildValues_includes_only_filled_fields()
    {
        var rows = TimeEntryCustomFieldSupport.CreateRows(
            [
                new TimeEntryCustomFieldDefinitionDto { Id = 1, Name = "A", Possible_Values = ["x"] },
                new TimeEntryCustomFieldDefinitionDto { Id = 2, Name = "B" }
            ],
            [],
            new AppSettings());

        rows[0].SelectedValue = "x";

        var values = TimeEntryCustomFieldSupport.BuildValues(rows);

        Assert.Single(values);
        Assert.Equal(1, values[0].Id);
        Assert.Equal("x", values[0].Value);
    }
}
