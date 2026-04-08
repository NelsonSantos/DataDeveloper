using DataDeveloper.NextGrid.Editors;

namespace DataDeveloper.NextGrid.UI;

public sealed class GridCellEditRequestedEventArgs : EventArgs
{
    public GridCellEditRequestedEventArgs(GridEditSession session, GridCellBounds bounds)
    {
        Session = session;
        Bounds = bounds;
    }

    public GridEditSession Session { get; }
    public GridCellBounds Bounds { get; }
}
