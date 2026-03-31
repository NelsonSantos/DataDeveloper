using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace DataDeveloper.Converters;

public class EnumValueToResourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var valueText = value?.ToString()?.ToLowerInvariant() ?? string.Empty;
        if (string.Equals(valueText, "postgressql", StringComparison.Ordinal))
            valueText = "postgresql";
        var result = $"/Assets/Svg/{valueText}.svg";
        return result;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
