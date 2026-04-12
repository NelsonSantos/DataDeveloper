using System;
using System.Globalization;
using Avalonia.Data.Converters;
using DataDeveloper.Data.Enums;

namespace DataDeveloper.Converters;

public class NodeTypeToIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not NodeType nodeType)
            return "\U000F024B";

        return nodeType switch
        {
            NodeType.Connection => "\U000F01BC",
            NodeType.Table => "\U000F04EB",
            NodeType.View => "\U000F0208",
            NodeType.Procedure => "\U000F0493",
            NodeType.Function => "\U000F0295",
            NodeType.Column => "\U000F08DF",
            NodeType.Parameter => "\U000F062E",
            _ => "\U000F024B"
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
