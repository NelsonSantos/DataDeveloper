namespace DataDeveloper.NextGrid.Editors;

public readonly record struct GridEditResult(
    GridCellAddress Cell,
    object? OriginalValue,
    object? NewValue,
    bool Committed);
