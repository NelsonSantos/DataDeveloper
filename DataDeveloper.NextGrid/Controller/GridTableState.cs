namespace DataDeveloper.NextGrid;

public readonly record struct GridTableState(
    GridViewportState Viewport,
    GridCellAddress? FocusCell,
    double HorizontalOffset,
    double VerticalOffset,
    int TopRowIndex,
    int VisibleRowCount,
    int RowCount,
    int ColumnCount);
