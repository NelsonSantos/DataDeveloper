namespace DataDeveloper.NextGrid;

public readonly record struct GridNavigationResult(
    GridCellAddress Cell,
    double HorizontalOffset,
    double VerticalOffset);
