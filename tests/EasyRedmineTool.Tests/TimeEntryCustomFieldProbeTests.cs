namespace EasyRedmineTool.Tests;

using EasyRedmineTool.Core;

public class TimeEntryCustomFieldProbeTests
{
    [Fact]
    public void ParseRequiredFieldNamesFromMessage_extracts_from_full_error_text()
    {
        var names = TimeEntryCustomFieldProbe.ParseRequiredFieldNamesFromMessage(
            "Zeiteintrag fehlgeschlagen: 422 Unprocessable Content {\"errors\":[\"Produktdaten Hierarchie darf nicht leer sein\"]}");

        Assert.Single(names);
        Assert.Equal("Produktdaten Hierarchie", names[0]);
    }

    [Theory]
    [InlineData("{\"errors\":[\"Produktdaten Hierarchie darf nicht leer sein\"]}", "Produktdaten Hierarchie")]
    [InlineData("{\"errors\":[\"Support Project cannot be blank\"]}", "Support Project")]
    [InlineData("{\"errors\":[\"Kostenstelle muss ausgefüllt werden\"]}", "Kostenstelle")]
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
