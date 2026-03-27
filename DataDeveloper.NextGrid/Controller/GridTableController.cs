using Avalonia.Input;

namespace DataDeveloper.NextGrid;

public sealed class GridTableController
{
    private readonly GridNavigationController _navigationController;
    private readonly GridColumnLayoutEngine _columnLayout;
    private readonly GridLayoutEngine _layoutEngine;
    private readonly GridViewportEngine _viewportEngine;
    private readonly GridSelectionModel _selection;
    private GridViewportState _viewport;
    private int _rowCount;
    private int _columnCount;

    public GridTableController(
        GridColumnLayoutEngine columnLayout,
        GridLayoutEngine layoutEngine,
        GridViewportEngine viewportEngine,
        GridSelectionModel selection)
    {
        _columnLayout = columnLayout ?? throw new ArgumentNullException(nameof(columnLayout));
        _layoutEngine = layoutEngine ?? throw new ArgumentNullException(nameof(layoutEngine));
        _viewportEngine = viewportEngine ?? throw new ArgumentNullException(nameof(viewportEngine));
        _selection = selection ?? throw new ArgumentNullException(nameof(selection));
        _navigationController = new GridNavigationController();
    }

    public GridTableState State => new(
        _viewport,
        _selection.FocusCell,
        _viewport.HorizontalOffset,
        _viewport.VerticalOffset,
        TopRowIndex,
        VisibleRowCount,
        _rowCount,
        _columnCount);

    public GridSelectionModel Selection => _selection;

    public int TopRowIndex => Math.Max(0, (int)Math.Floor(Math.Max(0, _viewport.VerticalOffset) / Math.Max(1, _viewport.RowHeight)));

    public int VisibleRowCount => GetVisibleRowCount();

    public void SetDimensions(int rowCount, int columnCount)
    {
        _rowCount = Math.Max(0, rowCount);
        _columnCount = Math.Max(0, columnCount);
    }

    public void UpdateViewport(GridViewportState viewport)
    {
        _viewport = viewport;
    }

    public GridViewportInfo CalculateViewport()
    {
        return _viewportEngine.CalculateViewport(_viewport, _rowCount);
    }

    public GridVisibleRange GetRowRenderRange(int overscan = 1)
    {
        if (_rowCount <= 0)
            return new GridVisibleRange(0, 0);

        var start = Math.Max(0, TopRowIndex - Math.Max(0, overscan));
        var visibleCount = Math.Max(1, VisibleRowCount);
        var end = Math.Min(_rowCount, TopRowIndex + visibleCount + Math.Max(0, overscan));
        return new GridVisibleRange(start, Math.Max(start, end));
    }

    public GridVisibleRange GetColumnRenderRange(int overscan = 1)
    {
        return _viewportEngine.GetVisibleColumns(_viewport, overscan);
    }

    public GridCellBounds GetCornerHeaderBounds()
    {
        return _layoutEngine.GetCornerHeaderBounds(_viewport.RowHeaderWidth, _viewport.HeaderHeight);
    }

    public GridCellBounds GetColumnHeaderBounds(int columnIndex)
    {
        return _layoutEngine.GetColumnHeaderBounds(
            columnIndex,
            _viewport.HorizontalOffset,
            _viewport.RowHeaderWidth,
            _viewport.HeaderHeight);
    }

    public GridCellBounds GetRowHeaderBounds(int rowIndex)
    {
        return _layoutEngine.GetRowHeaderBounds(
            rowIndex,
            _viewport.VerticalOffset,
            _viewport.RowHeaderWidth,
            _viewport.HeaderHeight,
            _viewport.RowHeight);
    }

    public GridCellBounds GetCellBounds(int rowIndex, int columnIndex)
    {
        return _layoutEngine.GetCellBounds(
            rowIndex,
            columnIndex,
            _viewport.HorizontalOffset,
            _viewport.VerticalOffset,
            _viewport.RowHeaderWidth,
            _viewport.HeaderHeight,
            _viewport.RowHeight);
    }

    public GridHitTestResult HitTest(double x, double y)
    {
        if (x < 0 || y < 0)
            return new GridHitTestResult(GridRegionKind.None, -1, -1);

        if (GetCornerHeaderBounds().Contains(x, y))
            return new GridHitTestResult(GridRegionKind.CornerHeader, -1, -1);

        var columns = GetColumnRenderRange();
        for (var columnIndex = columns.Start; columnIndex < columns.EndExclusive; columnIndex++)
        {
            var bounds = GetColumnHeaderBounds(columnIndex);
            if (bounds.Contains(x, y))
                return new GridHitTestResult(GridRegionKind.ColumnHeader, -1, columnIndex);
        }

        var rows = GetRowRenderRange();
        for (var rowIndex = rows.Start; rowIndex < rows.EndExclusive; rowIndex++)
        {
            var rowHeaderBounds = GetRowHeaderBounds(rowIndex);
            if (rowHeaderBounds.Contains(x, y))
                return new GridHitTestResult(GridRegionKind.RowHeader, rowIndex, -1);

            for (var columnIndex = columns.Start; columnIndex < columns.EndExclusive; columnIndex++)
            {
                var cellBounds = GetCellBounds(rowIndex, columnIndex);
                if (cellBounds.Contains(x, y))
                    return new GridHitTestResult(GridRegionKind.Cell, rowIndex, columnIndex);
            }
        }

        return _layoutEngine.HitTest(
            x,
            y,
            CalculateViewport(),
            _viewport.RowHeaderWidth,
            _viewport.HeaderHeight,
            _viewport.RowHeight);
    }

    public void HandlePointerSelection(GridHitTestResult hit, KeyModifiers modifiers)
    {
        if (hit.Region == GridRegionKind.None)
            return;

        if (hit.Region == GridRegionKind.ColumnHeader && hit.ColumnIndex >= 0 && hit.ColumnIndex < _columnCount)
        {
            if (modifiers.HasFlag(KeyModifiers.Shift))
                _selection.ExtendToColumn(hit.ColumnIndex, _rowCount);
            else
                _selection.SelectColumn(hit.ColumnIndex, _rowCount);
            return;
        }

        if (hit.Region == GridRegionKind.RowHeader && hit.RowIndex >= 0 && hit.RowIndex < _rowCount)
        {
            if (modifiers.HasFlag(KeyModifiers.Shift))
                _selection.ExtendToRow(hit.RowIndex, _columnCount);
            else
                _selection.SelectRow(hit.RowIndex, _columnCount);
            return;
        }

        if (hit.Region == GridRegionKind.Cell &&
            hit.RowIndex >= 0 && hit.RowIndex < _rowCount &&
            hit.ColumnIndex >= 0 && hit.ColumnIndex < _columnCount)
        {
            var cell = new GridCellAddress(hit.RowIndex, hit.ColumnIndex);
            if (modifiers.HasFlag(KeyModifiers.Shift))
                _selection.ExtendToCell(cell);
            else
                _selection.SelectCell(cell);
        }
    }

    public GridNavigationResult MoveFocus(GridNavigationDirection direction)
    {
        var current = _selection.FocusCell ?? new GridCellAddress(0, 0);
        var nextCell = _navigationController.Navigate(
            new GridNavigationRequest(current, direction, _rowCount, _columnCount));
        var result = EnsureVisible(nextCell, direction);

        _selection.SelectCell(result.Cell);
        _viewport = _viewport with
        {
            HorizontalOffset = result.HorizontalOffset,
            VerticalOffset = result.VerticalOffset
        };

        return result;
    }

    public GridNavigationResult EnsureVisible(GridCellAddress cell, GridNavigationDirection? direction = null)
    {
        var movedLeft = direction == GridNavigationDirection.Left;
        var movedRight = direction == GridNavigationDirection.Right;
        var movedUp = direction == GridNavigationDirection.Up;
        var movedDown = direction == GridNavigationDirection.Down;
        var viewportContentWidth = Math.Max(0, _viewport.ViewportWidth - _viewport.RowHeaderWidth);
        var horizontalOffset = _columnLayout.GetHorizontalOffsetForCell(
            _viewport.HorizontalOffset,
            viewportContentWidth,
            cell.Column,
            movedLeft,
            movedRight);
        var verticalOffset = GetVerticalOffsetForRow(cell.Row, movedUp, movedDown);

        return new GridNavigationResult(cell, horizontalOffset, verticalOffset);
    }

    private int GetVisibleRowCount()
    {
        var visibleHeight = Math.Max(0, _viewport.ViewportHeight - _viewport.HeaderHeight);
        return Math.Max(1, (int)Math.Floor(visibleHeight / Math.Max(1, _viewport.RowHeight)));
    }

    private double GetVerticalOffsetForRow(int rowIndex, bool movedUp, bool movedDown)
    {
        if (rowIndex < 0 || _viewport.RowHeight <= 0)
            return Math.Max(0, _viewport.VerticalOffset);

        var visibleRowCount = GetVisibleRowCount();
        var topRowIndex = TopRowIndex;
        var bottomRowIndexExclusive = topRowIndex + visibleRowCount;

        if (rowIndex < topRowIndex)
            return rowIndex * _viewport.RowHeight;

        if (rowIndex >= bottomRowIndexExclusive)
        {
            var targetTopRow = movedDown
                ? rowIndex - visibleRowCount + 1
                : rowIndex;

            return Math.Max(0, targetTopRow * _viewport.RowHeight);
        }

        return Math.Max(0, _viewport.VerticalOffset);
    }
}
