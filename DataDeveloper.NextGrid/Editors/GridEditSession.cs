namespace DataDeveloper.NextGrid.Editors;

public readonly record struct GridEditSession(
    GridCellAddress Cell,
    Type? ValueType,
    object? OriginalValue,
    object? CurrentValue);
