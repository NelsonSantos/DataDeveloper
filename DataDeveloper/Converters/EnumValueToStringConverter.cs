using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace DataDeveloper.Converters;

public class EnumValueToResourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // var uri = $"avares://DataDeveloper/Assets/Svg/{value?.ToString().ToLower()}.svg";
        // return new Uri(uri);
        var result = $"/Assets/Svg/{value?.ToString().ToLower()}.svg";
        return result;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}