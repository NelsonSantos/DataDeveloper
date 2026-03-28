namespace DataDeveloper.NextGrid;

public sealed class GridViewportEngine
{
    private readonly GridColumnLayoutEngine _columnLayout;

    public GridViewportEngine(GridColumnLayoutEngine columnLayout)
    {
        _columnLayout = columnLayout ?? throw new ArgumentNullException(nameof(columnLayout));
    }

    public GridViewportInfo CalculateViewport(GridViewportState state, int rowCount, int overscan = 1)
    {
        return new GridViewportInfo(
            GetVisibleRows(state, rowCount, overscan),
            GetVisibleColumns(state, overscan),
            Math.Max(0, state.HorizontalOffset),
            Math.Max(0, state.VerticalOffset));
    }

    public GridVisibleRange GetVisibleRows(GridViewportState state, int rowCount, int overscan = 1)
    {
        if (rowCount <= 0 || state.RowHeight <= 0)
            return new GridVisibleRange(0, 0);

        var contentOffset = Math.Max(0, state.VerticalOffset);
        var visibleHeight = Math.Max(0, state.ViewportHeight - state.HeaderHeight);
        var start = Math.Max(0, (int)Math.Floor(contentOffset / state.RowHeight) - overscan);
        var visibleCount = (int)Math.Ceiling(visibleHeight / state.RowHeight) + (overscan * 2) + 1;
        var end = Math.Min(rowCount, start + visibleCount);

        return new GridVisibleRange(start, end);
    }

    public GridVisibleRange GetVisibleColumns(GridViewportState state, int overscan = 1)
    {
        if (_columnLayout.Widths.Count == 0)
            return new GridVisibleRange(0, 0);

        var viewportContentWidth = Math.Max(0, state.ViewportWidth - state.RowHeaderWidth);
        var viewportLeft = Math.Max(0, state.HorizontalOffset);
        var viewportRight = viewportLeft + viewportContentWidth;

        var start = _columnLayout.GetColumnIndexAt(viewportLeft);
        if (start > 0)
            start = Math.Max(0, start - overscan);

        var end = _columnLayout.GetColumnIndexAt(viewportRight);
        end = Math.Min(_columnLayout.Widths.Count, end + overscan + 1);

        return new GridVisibleRange(start, Math.Max(start, end));
    }
}
