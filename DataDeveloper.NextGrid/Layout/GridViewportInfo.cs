namespace DataDeveloper.NextGrid;

public readonly record struct GridViewportInfo(
    GridVisibleRange Rows,
    GridVisibleRange Columns,
    double HorizontalOffset,
    double VerticalOffset);
