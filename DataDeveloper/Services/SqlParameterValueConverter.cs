using System;
using System.Globalization;

namespace DataDeveloper.Services;

public static class SqlParameterValueConverter
{
    public static object? Convert(string? value, bool isNull)
    {
        if (isNull)
            return null;

        if (value is null)
            return string.Empty;

        value = NormalizeValue(value);

        if (bool.TryParse(value, out var boolValue))
            return boolValue;

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
            return intValue;

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
            return longValue;

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue))
            return decimalValue;

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTimeValue))
            return dateTimeValue;

        if (DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out dateTimeValue))
            return dateTimeValue;

        return value;
    }

    private static string NormalizeValue(string value)
    {
        var normalized = value.Trim();
        if (normalized.Length >= 2)
        {
            var first = normalized[0];
            var last = normalized[^1];
            if ((first == '\'' && last == '\'') || (first == '"' && last == '"'))
                normalized = normalized[1..^1].Trim();
        }

        return normalized;
    }
}
