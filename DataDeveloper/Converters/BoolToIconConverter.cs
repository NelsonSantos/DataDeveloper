using System;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;

namespace DataDeveloper.Converters;

public class BoolToIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var icons = parameter?.ToString()?.Split("|") ?? ["", ""];
        var trueIcon = icons.ElementAtOrDefault(0) ?? "";
        var falseIcon = icons.ElementAtOrDefault(1) ?? "";
        var boolValue = value is bool b && b;
        return boolValue ? trueIcon : falseIcon;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
