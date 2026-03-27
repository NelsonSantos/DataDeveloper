namespace DataDeveloper.NextGrid.Editors;

public sealed class TextGridCellEditor : IGridCellEditor
{
    public bool CanEdit(Type? valueType, object? value)
    {
        return valueType == typeof(string) || value is string || valueType is null;
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
        return currentValue?.ToString() ?? string.Empty;
    }
}
