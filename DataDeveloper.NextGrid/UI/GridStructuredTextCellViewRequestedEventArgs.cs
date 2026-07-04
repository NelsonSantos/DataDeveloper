using DataDeveloper.NextGrid.Renderers;

namespace DataDeveloper.NextGrid.UI;

public sealed class GridStructuredTextCellViewRequestedEventArgs : EventArgs
{
    public GridStructuredTextCellViewRequestedEventArgs(GridCellAddress cell, string? value, bool isEditable, StructuredTextKind kind)
    {
        Cell = cell;
        Value = value;
        IsEditable = isEditable;
        Kind = kind;
    }

    public GridCellAddress Cell { get; }
    public string? Value { get; }
    public bool IsEditable { get; }
    public StructuredTextKind Kind { get; }
}
