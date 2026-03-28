namespace DataDeveloper.NextGrid.Editors;

public sealed class GridEditorRegistry
{
    private readonly List<IGridCellEditor> _editors = [];

    public GridEditorRegistry()
    {
        Register(new BooleanGridCellEditor());
        Register(new NumberGridCellEditor());
        Register(new DateTimeGridCellEditor());
        Register(new TextGridCellEditor());
        Register(new ObjectGridCellEditor());
    }

    public IReadOnlyList<IGridCellEditor> Editors => _editors;

    public void Register(IGridCellEditor editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        _editors.Add(editor);
    }

    public IGridCellEditor Resolve(Type? valueType, object? value)
    {
        for (var index = 0; index < _editors.Count; index++)
        {
            var editor = _editors[index];
            if (editor.CanEdit(valueType, value))
                return editor;
        }

        throw new InvalidOperationException("No editor registered for the provided value.");
    }
}
