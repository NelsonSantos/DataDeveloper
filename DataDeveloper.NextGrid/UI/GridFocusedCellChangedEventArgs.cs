namespace DataDeveloper.NextGrid.UI;

public sealed class GridFocusedCellChangedEventArgs : EventArgs
{
    public GridFocusedCellChangedEventArgs(GridCellAddress? cell)
    {
        Cell = cell;
    }

    public GridCellAddress? Cell { get; }
}
