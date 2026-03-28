namespace DataDeveloper.NextGrid.Renderers;

public sealed class TextGridCellRenderer : GridCellRendererBase
{
    public override GridColumnAlignment Alignment => GridColumnAlignment.Left;

    public override bool CanRender(Type? valueType, object? value)
    {
        return valueType == typeof(string) || value is string;
    }
}
