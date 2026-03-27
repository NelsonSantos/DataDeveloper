namespace DataDeveloper.NextGrid.Renderers;

public interface IGridCellRenderer
{
    GridColumnAlignment Alignment { get; }
    bool CanRender(Type? valueType, object? value);
    string FormatValue(object? value, GridRendererContext context);
    double MeasureWidth(object? value, GridRendererContext context, Func<string, double> measureText);
}
