namespace DataDeveloper.NextGrid;

public sealed class GridNavigationController
{
    public GridCellAddress Navigate(GridNavigationRequest request)
    {
        var nextCell = request.CurrentCell;

        switch (request.Direction)
        {
            case GridNavigationDirection.Up:
                nextCell = nextCell with { Row = Math.Max(0, nextCell.Row - Math.Max(1, request.Step)) };
                break;
            case GridNavigationDirection.Down:
                nextCell = nextCell with { Row = Math.Min(Math.Max(0, request.RowCount - 1), nextCell.Row + Math.Max(1, request.Step)) };
                break;
            case GridNavigationDirection.Left:
                nextCell = nextCell with { Column = Math.Max(0, nextCell.Column - Math.Max(1, request.Step)) };
                break;
            case GridNavigationDirection.Right:
                nextCell = nextCell with { Column = Math.Min(Math.Max(0, request.ColumnCount - 1), nextCell.Column + Math.Max(1, request.Step)) };
                break;
            case GridNavigationDirection.PageUp:
                nextCell = nextCell with { Row = Math.Max(0, nextCell.Row - Math.Max(1, request.Step)) };
                break;
            case GridNavigationDirection.PageDown:
                nextCell = nextCell with { Row = Math.Min(Math.Max(0, request.RowCount - 1), nextCell.Row + Math.Max(1, request.Step)) };
                break;
            case GridNavigationDirection.Home:
                nextCell = nextCell with { Column = 0 };
                break;
            case GridNavigationDirection.End:
                nextCell = nextCell with { Column = Math.Max(0, request.ColumnCount - 1) };
                break;
        }

        return nextCell;
    }
}
