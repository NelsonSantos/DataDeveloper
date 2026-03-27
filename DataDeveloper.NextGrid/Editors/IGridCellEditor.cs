namespace DataDeveloper.NextGrid.Editors;

public interface IGridCellEditor
{
    bool CanEdit(Type? valueType, object? value);
    object? BeginEdit(object? value);
    object? ApplyInput(object? currentValue, object? input);
    object? Commit(object? currentValue);
}
