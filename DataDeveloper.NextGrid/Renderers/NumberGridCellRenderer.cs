using System.Globalization;

namespace DataDeveloper.NextGrid.Renderers;

public sealed class NumberGridCellRenderer : GridCellRendererBase
{
    public override GridColumnAlignment Alignment => GridColumnAlignment.Right;

    public override bool CanRender(Type? valueType, object? value)
    {
        var type = Nullable.GetUnderlyingType(valueType ?? value?.GetType() ?? typeof(object)) ?? valueType ?? value?.GetType();
        if (type is null)
            return false;

        return type == typeof(byte) ||
               type == typeof(sbyte) ||
               type == typeof(short) ||
               type == typeof(ushort) ||
               type == typeof(int) ||
               type == typeof(uint) ||
               type == typeof(long) ||
               type == typeof(ulong) ||
               type == typeof(float) ||
               type == typeof(double) ||
               type == typeof(decimal);
    }

    public override string FormatValue(object? value, GridRendererContext context)
    {
        if (value is null)
            return string.Empty;

        return value switch
        {
            IFormattable formattable => formattable.ToString(null, context.Culture),
            _ => Convert.ToString(value, context.Culture) ?? string.Empty
        };
    }
}
