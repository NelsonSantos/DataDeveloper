namespace DataDeveloper.NextGrid;

public readonly record struct GridViewportState(
    double HorizontalOffset,
    double VerticalOffset,
    double ViewportWidth,
    double ViewportHeight,
    double RowHeaderWidth,
    double HeaderHeight,
    double RowHeight);
