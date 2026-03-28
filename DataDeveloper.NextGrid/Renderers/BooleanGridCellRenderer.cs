namespace DataDeveloper.NextGrid.Renderers;

public sealed class BooleanGridCellRenderer : GridCellRendererBase
{
    public override GridColumnAlignment Alignment => GridColumnAlignment.Center;

    public override bool CanRender(Type? valueType, object? value)
    {
        var type = Nullable.GetUnderlyingType(valueType ?? value?.GetType() ?? typeof(object)) ?? valueType ?? value?.GetType();
        return type == typeof(bool);
    }

    public override string FormatValue(object? value, GridRendererContext context)
    {
        return value is bool boolean ? boolean.ToString() : string.Empty;
    }
}
