namespace DataDeveloper.NextGrid;

public sealed class GridSelectionModel
{
    private readonly List<GridSelectionRange> _ranges = [];

    public GridCellAddress? AnchorCell { get; private set; }
    public GridCellAddress? FocusCell { get; private set; }
    public IReadOnlyList<GridSelectionRange> Ranges => _ranges;

    public void Clear()
    {
        _ranges.Clear();
        AnchorCell = null;
        FocusCell = null;
    }

    public void SelectCell(GridCellAddress cell)
    {
        _ranges.Clear();
        _ranges.Add(new GridSelectionRange(cell, cell));
        AnchorCell = cell;
        FocusCell = cell;
    }

    public void SelectRange(GridCellAddress anchor, GridCellAddress focus)
    {
        _ranges.Clear();
        _ranges.Add(new GridSelectionRange(anchor, focus));
        AnchorCell = anchor;
        FocusCell = focus;
    }

    public void SelectRow(int rowIndex, int columnCount)
    {
        if (columnCount <= 0)
        {
            Clear();
            return;
        }

        SelectRange(new GridCellAddress(rowIndex, 0), new GridCellAddress(rowIndex, columnCount - 1));
    }

    public void SelectRows(int startRowIndex, int endRowIndex, int columnCount)
    {
        if (columnCount <= 0)
        {
            Clear();
            return;
        }

        SelectRange(
            new GridCellAddress(Math.Min(startRowIndex, endRowIndex), 0),
            new GridCellAddress(Math.Max(startRowIndex, endRowIndex), columnCount - 1));
    }

    public void ExtendToCell(GridCellAddress focus)
    {
        var anchor = AnchorCell ?? focus;
        SelectRange(anchor, focus);
    }

    public void ExtendToRow(int rowIndex, int columnCount)
    {
        if (columnCount <= 0)
        {
            Clear();
            return;
        }

        var anchor = AnchorCell ?? new GridCellAddress(rowIndex, 0);
        SelectRange(
            new GridCellAddress(anchor.Row, 0),
            new GridCellAddress(rowIndex, columnCount - 1));
    }

    public void SelectColumn(int columnIndex, int loadedRowCount)
    {
        if (loadedRowCount <= 0)
        {
            Clear();
            return;
        }

        SelectRange(new GridCellAddress(0, columnIndex), new GridCellAddress(loadedRowCount - 1, columnIndex));
    }

    public void SelectColumns(int startColumnIndex, int endColumnIndex, int loadedRowCount)
    {
        if (loadedRowCount <= 0)
        {
            Clear();
            return;
        }

        SelectRange(
            new GridCellAddress(0, Math.Min(startColumnIndex, endColumnIndex)),
            new GridCellAddress(loadedRowCount - 1, Math.Max(startColumnIndex, endColumnIndex)));
    }

    public void ExtendToColumn(int columnIndex, int loadedRowCount)
    {
        if (loadedRowCount <= 0)
        {
            Clear();
            return;
        }

        var anchor = AnchorCell ?? new GridCellAddress(0, columnIndex);
        SelectRange(
            new GridCellAddress(0, anchor.Column),
            new GridCellAddress(loadedRowCount - 1, columnIndex));
    }

    public bool Contains(GridCellAddress cell)
    {
        for (var index = 0; index < _ranges.Count; index++)
        {
            if (_ranges[index].Contains(cell))
                return true;
        }

        return false;
    }
}
