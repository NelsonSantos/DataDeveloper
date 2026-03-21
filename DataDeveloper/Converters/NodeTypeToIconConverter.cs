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
            return "\uf07b";

        return nodeType switch
        {
            NodeType.Connection => "\uf1c0",
            NodeType.Table => "\uf00b",
            NodeType.Column => "\uf0ca",
            _ => "\uf07b"
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
