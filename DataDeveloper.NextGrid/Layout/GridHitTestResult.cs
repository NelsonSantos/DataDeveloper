namespace DataDeveloper.NextGrid;

public readonly record struct GridHitTestResult(
    GridRegionKind Region,
    int RowIndex,
    int ColumnIndex);
