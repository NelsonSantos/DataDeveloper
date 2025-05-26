using System;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace DataDeveloper.Converters;

public class BoolToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool val = value is bool b && b;

        // Suporte a parâmetro do tipo string (ex: "Red|Green")
        if (parameter is string colors)
        {
            var parts = colors.Split('|');
            var trueColor = parts.ElementAtOrDefault(0) ?? "Red";
            var falseColor = parts.ElementAtOrDefault(1) ?? "Transparent";

            return val ? Brush.Parse(trueColor) : Brush.Parse(falseColor);
        }

        // padrão
        return val ? Brushes.Red : Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}