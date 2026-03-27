namespace DataDeveloper.NextGrid.Editors;

public sealed class ObjectGridCellEditor : IGridCellEditor
{
    public bool CanEdit(Type? valueType, object? value)
    {
        return true;
    }

    public object? BeginEdit(object? value)
    {
        return value?.ToString() ?? string.Empty;
    }

    public object? ApplyInput(object? currentValue, object? input)
    {
        return input;
    }

    public object? Commit(object? currentValue)
    {
        return currentValue;
    }
}
