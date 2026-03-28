namespace DataDeveloper.NextGrid.Renderers;

public sealed class GridRendererRegistry
{
    private readonly List<IGridCellRenderer> _renderers = [];

    public GridRendererRegistry()
    {
        Register(new NullGridCellRenderer());
        Register(new BooleanGridCellRenderer());
        Register(new NumberGridCellRenderer());
        Register(new DateTimeGridCellRenderer());
        Register(new TextGridCellRenderer());
        Register(new ObjectGridCellRenderer());
    }

    public IReadOnlyList<IGridCellRenderer> Renderers => _renderers;

    public void Register(IGridCellRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        _renderers.Add(renderer);
    }

    public IGridCellRenderer Resolve(Type? valueType, object? value)
    {
        for (var index = 0; index < _renderers.Count; index++)
        {
            var renderer = _renderers[index];
            if (renderer.CanRender(valueType, value))
                return renderer;
        }

        throw new InvalidOperationException("No renderer registered for the provided value.");
    }
}
