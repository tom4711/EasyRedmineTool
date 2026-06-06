namespace EasyRedmineTool.Tests;

using EasyRedmineTool.Core;

public class RedmineDatesTests
{
    [Theory]
    [InlineData("2025-06-06", 2025, 6, 6)]
    [InlineData("2024-01-01", 2024, 1, 1)]
    public void TryParseSpentOn_parses_valid_dates(string input, int year, int month, int day)
    {
        var result = RedmineDates.TryParseSpentOn(input);

        Assert.NotNull(result);
        Assert.Equal(new DateTime(year, month, day), result.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("06.06.2025")]
    [InlineData("invalid")]
    public void TryParseSpentOn_returns_null_for_invalid_input(string? input)
    {
        Assert.Null(RedmineDates.TryParseSpentOn(input));
    }

    [Fact]
    public void FormatSpentOn_round_trips_with_parser()
    {
        var date = new DateTime(2025, 3, 15);
        var formatted = RedmineDates.FormatSpentOn(date);

        Assert.Equal("2025-03-15", formatted);
        Assert.Equal(date, RedmineDates.TryParseSpentOn(formatted));
    }

    [Fact]
    public void TodayKey_matches_today()
    {
        Assert.Equal(RedmineDates.FormatSpentOn(DateTime.Today), RedmineDates.TodayKey());
    }
}
