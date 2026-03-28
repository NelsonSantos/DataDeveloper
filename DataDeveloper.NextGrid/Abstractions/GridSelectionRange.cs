namespace DataDeveloper.NextGrid;

public readonly record struct GridSelectionRange(GridCellAddress Start, GridCellAddress End)
{
    public int TopRow => Math.Min(Start.Row, End.Row);
    public int BottomRow => Math.Max(Start.Row, End.Row);
    public int LeftColumn => Math.Min(Start.Column, End.Column);
    public int RightColumn => Math.Max(Start.Column, End.Column);

    public bool Contains(GridCellAddress cell)
    {
        return cell.Row >= TopRow &&
               cell.Row <= BottomRow &&
               cell.Column >= LeftColumn &&
               cell.Column <= RightColumn;
    }
}
