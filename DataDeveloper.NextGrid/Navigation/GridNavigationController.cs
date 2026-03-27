namespace DataDeveloper.NextGrid;

public sealed class GridNavigationController
{
    public GridCellAddress Navigate(GridNavigationRequest request)
    {
        var nextCell = request.CurrentCell;

        switch (request.Direction)
        {
            case GridNavigationDirection.Up:
                nextCell = nextCell with { Row = Math.Max(0, nextCell.Row - 1) };
                break;
            case GridNavigationDirection.Down:
                nextCell = nextCell with { Row = Math.Min(Math.Max(0, request.RowCount - 1), nextCell.Row + 1) };
                break;
            case GridNavigationDirection.Left:
                nextCell = nextCell with { Column = Math.Max(0, nextCell.Column - 1) };
                break;
            case GridNavigationDirection.Right:
                nextCell = nextCell with { Column = Math.Min(Math.Max(0, request.ColumnCount - 1), nextCell.Column + 1) };
                break;
        }

        return nextCell;
    }
}
