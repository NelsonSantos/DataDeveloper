using Avalonia;

namespace DataDeveloper.NextGrid.Renderers;

public sealed class TextGridCellRenderer : GridCellRendererBase
{
    public const double IconSize = 16;
    public const double IconMargin = 4;

    public static double ReservedWidth => IconSize + (IconMargin * 2);

    public override GridColumnAlignment Alignment => GridColumnAlignment.Left;

    public override bool CanRender(Type? valueType, object? value)
    {
        return valueType == typeof(string) || value is string;
    }

    public static Rect GetIconRect(GridCellBounds bounds)
    {
        var x = bounds.X + bounds.Width - IconSize - IconMargin;
        var y = bounds.Y + ((bounds.Height - IconSize) / 2);
        return new Rect(x, y, IconSize, IconSize);
    }

    public override double MeasureWidth(object? value, GridRendererContext context, Func<string, double> measureText)
    {
        return base.MeasureWidth(value, context, measureText) + ReservedWidth;
    }
}
