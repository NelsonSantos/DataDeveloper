using System.Globalization;

namespace DataDeveloper.NextGrid;

internal static class GridValueFormats
{
    private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";
    private const string DateTimeOffsetFormat = "yyyy-MM-dd HH:mm:ss zzz";

    public static CultureInfo Culture => CultureInfo.InvariantCulture;

    public static string FormatNumber(object value)
    {
        return value switch
        {
            IFormattable formattable => formattable.ToString(null, Culture),
            _ => Convert.ToString(value, Culture) ?? string.Empty
        };
    }

    public static string FormatDateTime(DateTime value)
    {
        return value.ToString(DateTimeFormat, Culture);
    }

    public static string FormatDateTimeOffset(DateTimeOffset value)
    {
        return value.ToString(DateTimeOffsetFormat, Culture);
    }

    public static bool TryParseDecimal(string text, out decimal value)
    {
        var normalized = NormalizeDecimal(text);
        return decimal.TryParse(normalized, NumberStyles.Number, Culture, out value);
    }

    public static bool TryParseDateTime(string text, out DateTime value)
    {
        if (DateTime.TryParseExact(text, DateTimeFormat, Culture, DateTimeStyles.None, out value))
            return true;

        return DateTime.TryParse(text, Culture, DateTimeStyles.None, out value);
    }

    public static bool TryParseDateTimeOffset(string text, out DateTimeOffset value)
    {
        if (DateTimeOffset.TryParseExact(text, DateTimeOffsetFormat, Culture, DateTimeStyles.None, out value))
            return true;

        return DateTimeOffset.TryParse(text, Culture, DateTimeStyles.None, out value);
    }

    private static string NormalizeDecimal(string text)
    {
        var trimmed = text.Trim().Replace(" ", string.Empty, StringComparison.Ordinal);
        var lastComma = trimmed.LastIndexOf(',');
        var lastDot = trimmed.LastIndexOf('.');

        if (lastComma >= 0 && lastDot >= 0)
        {
            if (lastComma > lastDot)
                return trimmed.Replace(".", string.Empty, StringComparison.Ordinal).Replace(',', '.');

            return trimmed.Replace(",", string.Empty, StringComparison.Ordinal);
        }

        if (lastComma >= 0)
            return trimmed.Replace(',', '.');

        return trimmed;
    }
}
