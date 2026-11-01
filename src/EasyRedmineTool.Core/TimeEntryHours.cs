namespace EasyRedmineTool.Core;

using System.Globalization;

public static class TimeEntryHours
{
    public static bool TryParsePositive(string? value, out double hours)
    {
        hours = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var input = value.Trim();
        var decimalSeparatorCount = 0;
        foreach (var character in input)
        {
            if (character is '.' or ',')
            {
                decimalSeparatorCount++;
                continue;
            }

            if (!char.IsAsciiDigit(character))
            {
                return false;
            }
        }

        if (decimalSeparatorCount > 1)
        {
            return false;
        }

        var normalized = input.Replace(',', '.');
        return double.TryParse(
                   normalized,
                   NumberStyles.AllowDecimalPoint,
                   CultureInfo.InvariantCulture,
                   out hours) &&
               double.IsFinite(hours) &&
               hours > 0;
    }
}
