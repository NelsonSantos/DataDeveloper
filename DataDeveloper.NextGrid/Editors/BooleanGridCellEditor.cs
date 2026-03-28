namespace DataDeveloper.NextGrid.Editors;

public sealed class BooleanGridCellEditor : IGridCellEditor
{
    public bool CanEdit(Type? valueType, object? value)
    {
        var type = Nullable.GetUnderlyingType(valueType ?? value?.GetType() ?? typeof(object)) ?? valueType ?? value?.GetType();
        return type == typeof(bool);
    }

    public object? BeginEdit(object? value)
    {
        return value is bool boolean && boolean;
    }

    public object? ApplyInput(object? currentValue, object? input)
    {
        return input switch
        {
            bool boolean => boolean,
            string text when bool.TryParse(text, out var parsed) => parsed,
            _ => currentValue ?? false
        };
    }

    public object? Commit(object? currentValue)
    {
        return currentValue switch
        {
            bool boolean => boolean,
            _ => false
        };
    }
}
