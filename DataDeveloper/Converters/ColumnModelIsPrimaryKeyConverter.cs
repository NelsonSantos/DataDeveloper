using System;
using System.Globalization;
using Avalonia.Data.Converters;
using DataDeveloper.Data.Models;

namespace DataDeveloper.Converters;

public class ColumnModelIsPrimaryKeyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ColumnModel cm)
            return cm.IsPrimaryKey;

        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}