using DataDeveloper.NextGrid;
using Xunit;

namespace DataDeveloper.Tests.NextGrid;

public sealed class GridSelectionModelTests
{
    [Fact]
    public void SelectCell_SetsAnchorFocusAndSingleRange()
    {
        var selection = new GridSelectionModel();

        selection.SelectCell(new GridCellAddress(2, 3));

        Assert.Equal(new GridCellAddress(2, 3), selection.AnchorCell);
        Assert.Equal(new GridCellAddress(2, 3), selection.FocusCell);
        Assert.Single(selection.Ranges);
        Assert.True(selection.Contains(new GridCellAddress(2, 3)));
    }

    [Fact]
    public void SelectRows_SelectsEntireRowBand()
    {
        var selection = new GridSelectionModel();

        selection.SelectRows(2, 4, 5);

        Assert.True(selection.Contains(new GridCellAddress(2, 0)));
        Assert.True(selection.Contains(new GridCellAddress(3, 2)));
        Assert.True(selection.Contains(new GridCellAddress(4, 4)));
        Assert.False(selection.Contains(new GridCellAddress(1, 2)));
        Assert.False(selection.Contains(new GridCellAddress(5, 2)));
    }

    [Fact]
    public void SelectColumns_UsesOnlyLoadedRows()
    {
        var selection = new GridSelectionModel();

        selection.SelectColumns(1, 2, loadedRowCount: 3);

        Assert.True(selection.Contains(new GridCellAddress(0, 1)));
        Assert.True(selection.Contains(new GridCellAddress(2, 2)));
        Assert.False(selection.Contains(new GridCellAddress(3, 1)));
        Assert.False(selection.Contains(new GridCellAddress(1, 0)));
    }

    [Fact]
    public void ExtendToCell_UsesAnchorToCreateRectangularSelection()
    {
        var selection = new GridSelectionModel();

        selection.SelectCell(new GridCellAddress(1, 1));
        selection.ExtendToCell(new GridCellAddress(3, 4));

        Assert.True(selection.Contains(new GridCellAddress(1, 1)));
        Assert.True(selection.Contains(new GridCellAddress(2, 3)));
        Assert.True(selection.Contains(new GridCellAddress(3, 4)));
        Assert.False(selection.Contains(new GridCellAddress(0, 0)));
    }

    [Fact]
    public void ExtendToRow_UsesAnchorRow()
    {
        var selection = new GridSelectionModel();

        selection.SelectRow(2, 4);
        selection.ExtendToRow(4, 4);

        Assert.True(selection.Contains(new GridCellAddress(2, 0)));
        Assert.True(selection.Contains(new GridCellAddress(3, 2)));
        Assert.True(selection.Contains(new GridCellAddress(4, 3)));
        Assert.False(selection.Contains(new GridCellAddress(1, 1)));
    }

    [Fact]
    public void ExtendToColumn_UsesAnchorColumnAndLoadedRows()
    {
        var selection = new GridSelectionModel();

        selection.SelectColumn(1, 3);
        selection.ExtendToColumn(3, 3);

        Assert.True(selection.Contains(new GridCellAddress(0, 1)));
        Assert.True(selection.Contains(new GridCellAddress(2, 2)));
        Assert.True(selection.Contains(new GridCellAddress(1, 3)));
        Assert.False(selection.Contains(new GridCellAddress(3, 2)));
    }

    [Fact]
    public void SelectAll_SelectsWholeGrid()
    {
        var selection = new GridSelectionModel();

        selection.SelectAll(rowCount: 3, columnCount: 4);

        Assert.True(selection.Contains(new GridCellAddress(0, 0)));
        Assert.True(selection.Contains(new GridCellAddress(2, 3)));
        Assert.False(selection.Contains(new GridCellAddress(3, 0)));
    }
}
