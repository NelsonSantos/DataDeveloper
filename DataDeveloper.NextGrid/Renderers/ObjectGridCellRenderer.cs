namespace DataDeveloper.NextGrid.Renderers;

public sealed class ObjectGridCellRenderer : GridCellRendererBase
{
    public override GridColumnAlignment Alignment => GridColumnAlignment.Left;

    public override bool CanRender(Type? valueType, object? value)
    {
        return true;
    }
}
