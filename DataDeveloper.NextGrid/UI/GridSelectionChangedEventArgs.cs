namespace DataDeveloper.NextGrid.UI;

public sealed class GridSelectionChangedEventArgs : EventArgs
{
    public GridSelectionChangedEventArgs(GridCellAddress? focusCell, IReadOnlyList<GridSelectionRange> ranges)
    {
        FocusCell = focusCell;
        Ranges = ranges;
    }

    public GridCellAddress? FocusCell { get; }

    public IReadOnlyList<GridSelectionRange> Ranges { get; }
}
