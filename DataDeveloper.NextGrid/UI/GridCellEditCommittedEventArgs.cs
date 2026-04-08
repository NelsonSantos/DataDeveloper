using DataDeveloper.NextGrid.Editors;

namespace DataDeveloper.NextGrid.UI;

public sealed class GridCellEditCommittedEventArgs : EventArgs
{
    public GridCellEditCommittedEventArgs(GridEditResult result)
    {
        Result = result;
    }

    public GridEditResult Result { get; }
}
