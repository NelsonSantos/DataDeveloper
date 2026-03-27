namespace DataDeveloper.NextGrid;

public sealed class GridLayoutEngine
{
    private readonly GridColumnLayoutEngine _columnLayout;

    public GridLayoutEngine(GridColumnLayoutEngine columnLayout)
    {
        _columnLayout = columnLayout ?? throw new ArgumentNullException(nameof(columnLayout));
    }

    public GridCellBounds GetCornerHeaderBounds(double rowHeaderWidth, double headerHeight)
    {
        return new GridCellBounds(GridRegionKind.CornerHeader, -1, -1, 0, 0, rowHeaderWidth, headerHeight);
    }

    public GridCellBounds GetColumnHeaderBounds(
        int columnIndex,
        double horizontalOffset,
        double rowHeaderWidth,
        double headerHeight)
    {
        var x = rowHeaderWidth + _columnLayout.GetColumnStart(columnIndex) - horizontalOffset;
        return new GridCellBounds(
            GridRegionKind.ColumnHeader,
            -1,
            columnIndex,
            x,
            0,
            _columnLayout.Widths[columnIndex],
            headerHeight);
    }

    public GridCellBounds GetRowHeaderBounds(
        int rowIndex,
        double verticalOffset,
        double rowHeaderWidth,
        double headerHeight,
        double rowHeight)
    {
        var y = headerHeight + (rowIndex * rowHeight) - verticalOffset;
        return new GridCellBounds(
            GridRegionKind.RowHeader,
            rowIndex,
            -1,
            0,
            y,
            rowHeaderWidth,
            rowHeight);
    }

    public GridCellBounds GetCellBounds(
        int rowIndex,
        int columnIndex,
        double horizontalOffset,
        double verticalOffset,
        double rowHeaderWidth,
        double headerHeight,
        double rowHeight)
    {
        var x = rowHeaderWidth + _columnLayout.GetColumnStart(columnIndex) - horizontalOffset;
        var y = headerHeight + (rowIndex * rowHeight) - verticalOffset;

        return new GridCellBounds(
            GridRegionKind.Cell,
            rowIndex,
            columnIndex,
            x,
            y,
            _columnLayout.Widths[columnIndex],
            rowHeight);
    }

    public GridHitTestResult HitTest(
        double x,
        double y,
        GridViewportInfo viewport,
        double rowHeaderWidth,
        double headerHeight,
        double rowHeight)
    {
        if (x < 0 || y < 0)
            return new GridHitTestResult(GridRegionKind.None, -1, -1);

        if (x <= rowHeaderWidth && y <= headerHeight)
            return new GridHitTestResult(GridRegionKind.CornerHeader, -1, -1);

        if (y <= headerHeight)
        {
            var contentX = x + viewport.HorizontalOffset - rowHeaderWidth;
            var columnIndex = _columnLayout.GetColumnIndexAt(contentX);
            return columnIndex < 0
                ? new GridHitTestResult(GridRegionKind.None, -1, -1)
                : new GridHitTestResult(GridRegionKind.ColumnHeader, -1, columnIndex);
        }

        var rowIndex = (int)Math.Floor((y + viewport.VerticalOffset - headerHeight) / rowHeight);
        if (rowIndex < 0)
            return new GridHitTestResult(GridRegionKind.None, -1, -1);

        if (x <= rowHeaderWidth)
            return new GridHitTestResult(GridRegionKind.RowHeader, rowIndex, -1);

        var cellContentX = x + viewport.HorizontalOffset - rowHeaderWidth;
        var cellColumnIndex = _columnLayout.GetColumnIndexAt(cellContentX);
        if (cellColumnIndex < 0)
            return new GridHitTestResult(GridRegionKind.None, -1, -1);

        return new GridHitTestResult(GridRegionKind.Cell, rowIndex, cellColumnIndex);
    }
}
