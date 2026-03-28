namespace DataDeveloper.NextGrid;

public readonly record struct GridCellBounds(
    GridRegionKind Region,
    int RowIndex,
    int ColumnIndex,
    double X,
    double Y,
    double Width,
    double Height)
{
    public bool Contains(double x, double y)
    {
        return x >= X &&
               y >= Y &&
               x < X + Width &&
               y < Y + Height;
    }
}
