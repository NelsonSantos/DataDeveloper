namespace DataDeveloper.NextGrid.Editors;

public sealed class GridEditorHost
{
    private readonly GridEditorRegistry _registry;

    public GridEditorHost(GridEditorRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public GridEditSession? CurrentSession { get; private set; }

    public GridEditSession BeginEdit(GridCellAddress cell, Type? valueType, object? value)
    {
        var editor = _registry.Resolve(valueType, value);
        var currentValue = editor.BeginEdit(value);
        var session = new GridEditSession(cell, valueType, value, currentValue);
        CurrentSession = session;
        return session;
    }

    public GridEditSession ApplyInput(object? input)
    {
        if (CurrentSession is null)
            throw new InvalidOperationException("There is no active edit session.");

        var session = CurrentSession.Value;
        var editor = _registry.Resolve(session.ValueType, session.OriginalValue);
        session = session with { CurrentValue = editor.ApplyInput(session.CurrentValue, input) };
        CurrentSession = session;
        return session;
    }

    public GridEditResult Commit()
    {
        if (CurrentSession is null)
            throw new InvalidOperationException("There is no active edit session.");

        var session = CurrentSession.Value;
        var editor = _registry.Resolve(session.ValueType, session.OriginalValue);
        var committedValue = editor.Commit(session.CurrentValue);
        CurrentSession = null;

        return new GridEditResult(session.Cell, session.OriginalValue, committedValue, Committed: true);
    }

    public GridEditResult Cancel()
    {
        if (CurrentSession is null)
            throw new InvalidOperationException("There is no active edit session.");

        var session = CurrentSession.Value;
        CurrentSession = null;

        return new GridEditResult(session.Cell, session.OriginalValue, session.OriginalValue, Committed: false);
    }
}
