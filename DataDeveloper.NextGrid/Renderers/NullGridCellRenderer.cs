namespace DataDeveloper.NextGrid.Renderers;

public sealed class NullGridCellRenderer : GridCellRendererBase
{
    public override GridColumnAlignment Alignment => GridColumnAlignment.Left;

    public override bool CanRender(Type? valueType, object? value)
    {
        return value is null || value is DBNull;
    }
}
