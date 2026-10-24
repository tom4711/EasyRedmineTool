namespace EasyRedmineTool.Tests;

using EasyRedmineTool.Core;

public class TimeEntryCustomFieldProbeTests
{
    [Fact]
    public void ParseRequiredFieldNamesFromMessage_extracts_from_full_error_text()
    {
        var names = TimeEntryCustomFieldProbe.ParseRequiredFieldNamesFromMessage(
            $$"""Zeiteintrag fehlgeschlagen: 422 Unprocessable Content {"errors":["{{CustomFieldTestData.ListFieldName}} cannot be blank"]}""");

        Assert.Single(names);
        Assert.Equal(CustomFieldTestData.ListFieldName, names[0]);
    }

    [Theory]
    [InlineData("{\"errors\":[\"Product Category cannot be blank\"]}", "Product Category")]
    [InlineData("{\"errors\":[\"Support Project cannot be blank\"]}", "Support Project")]
    [InlineData("{\"errors\":[\"Cost center cannot be blank\"]}", "Cost center")]
    public void ParseRequiredFieldNames_extracts_field_name(string body, string expectedName)
    {
        var names = TimeEntryCustomFieldProbe.ParseRequiredFieldNames(body);

        Assert.Single(names);
        Assert.Equal(expectedName, names[0]);
    }

    [Fact]
    public void ParseRequiredFieldNames_ignores_unrelated_errors()
    {
        var names = TimeEntryCustomFieldProbe.ParseRequiredFieldNames(
            "{\"errors\":[\"Hours must be greater than 0\"]}");

        Assert.Empty(names);
    }
}
