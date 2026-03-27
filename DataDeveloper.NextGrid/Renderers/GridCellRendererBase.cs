namespace DataDeveloper.NextGrid.Renderers;

public abstract class GridCellRendererBase : IGridCellRenderer
{
    public abstract GridColumnAlignment Alignment { get; }

    public abstract bool CanRender(Type? valueType, object? value);

    public double MeasureWidth(object? value, GridRendererContext context, Func<string, double> measureText)
    {
        ArgumentNullException.ThrowIfNull(measureText);
        return measureText(FormatValue(value, context));
    }

    public virtual string FormatValue(object? value, GridRendererContext context)
    {
        return value?.ToString() ?? string.Empty;
    }
}
