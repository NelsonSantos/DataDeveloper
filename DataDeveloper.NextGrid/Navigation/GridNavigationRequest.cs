namespace DataDeveloper.NextGrid;

public readonly record struct GridNavigationRequest(
    GridCellAddress CurrentCell,
    GridNavigationDirection Direction,
    int RowCount,
    int ColumnCount);
