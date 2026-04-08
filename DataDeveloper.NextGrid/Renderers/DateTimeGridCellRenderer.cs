namespace DataDeveloper.NextGrid.Renderers;

public sealed class DateTimeGridCellRenderer : GridCellRendererBase
{
    public override GridColumnAlignment Alignment => GridColumnAlignment.Left;

    public override bool CanRender(Type? valueType, object? value)
    {
        var type = Nullable.GetUnderlyingType(valueType ?? value?.GetType() ?? typeof(object)) ?? valueType ?? value?.GetType();
        return type == typeof(DateTime) || type == typeof(DateTimeOffset);
    }

    public override string FormatValue(object? value, GridRendererContext context)
    {
        return value switch
        {
            DateTime dateTime => GridValueFormats.FormatDateTime(dateTime),
            DateTimeOffset dateTimeOffset => GridValueFormats.FormatDateTimeOffset(dateTimeOffset),
            _ => string.Empty
        };
    }
}
