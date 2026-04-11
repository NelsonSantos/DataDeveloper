namespace DataDeveloper.NextGrid.UI;

internal static class GridSelectionStatusFormatter
{
    public static string Format(GridCellAddress? focusCell, IReadOnlyList<GridSelectionRange> ranges)
    {
        if (focusCell is null)
            return "Cell=nothing";

        if (TryGetSelectedSize(ranges, out var rowCount, out var columnCount) &&
            (rowCount > 1 || columnCount > 1))
        {
            return $"Selected={rowCount:N0} row(s) x {columnCount:N0} column(s)";
        }

        return $"Cell={focusCell.Value.Row + 1:N0}:{focusCell.Value.Column + 1:N0}";
    }

    private static bool TryGetSelectedSize(IReadOnlyList<GridSelectionRange> ranges, out int rowCount, out int columnCount)
    {
        rowCount = 0;
        columnCount = 0;

        if (ranges.Count == 0)
            return false;

        var topRow = ranges.Min(range => range.TopRow);
        var bottomRow = ranges.Max(range => range.BottomRow);
        var leftColumn = ranges.Min(range => range.LeftColumn);
        var rightColumn = ranges.Max(range => range.RightColumn);

        rowCount = bottomRow - topRow + 1;
        columnCount = rightColumn - leftColumn + 1;
        return rowCount > 0 && columnCount > 0;
    }
}
