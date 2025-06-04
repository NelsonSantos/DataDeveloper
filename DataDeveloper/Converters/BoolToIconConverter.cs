using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace DataDeveloper.Converters;

public class BoolToIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var icons = parameter.ToString().Split("|");
        var boolValue = (bool)value;
        return $"{(boolValue ? icons[0] : icons[1])}";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}