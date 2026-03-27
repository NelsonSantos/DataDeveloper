using System.Globalization;

namespace DataDeveloper.NextGrid.Editors;

public sealed class NumberGridCellEditor : IGridCellEditor
{
    public bool CanEdit(Type? valueType, object? value)
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

    public object? BeginEdit(object? value)
    {
        return value?.ToString() ?? string.Empty;
    }

    public object? ApplyInput(object? currentValue, object? input)
    {
        return input?.ToString() ?? string.Empty;
    }

    public object? Commit(object? currentValue)
    {
        if (currentValue is null)
            return null;

        var text = currentValue.ToString() ?? string.Empty;
        if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out var parsed))
            return parsed;

        throw new FormatException($"Invalid numeric value '{text}'.");
    }
}
