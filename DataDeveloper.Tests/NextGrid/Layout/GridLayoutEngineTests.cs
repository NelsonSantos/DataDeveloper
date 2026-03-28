using DataDeveloper.NextGrid;
using Xunit;

namespace DataDeveloper.Tests.NextGrid.Layout;

public sealed class GridLayoutEngineTests
{
    [Fact]
    public void GetCellBounds_UsesColumnAndRowOffsets()
    {
        var columns = new GridColumnLayoutEngine(100);
        columns.EnsureColumnCount(3);
        columns.TrackWidth(0, 120);
        columns.TrackWidth(1, 140);
        columns.TrackWidth(2, 160);
        var layout = new GridLayoutEngine(columns);

        var bounds = layout.GetCellBounds(
            rowIndex: 2,
            columnIndex: 1,
            horizontalOffset: 30,
            verticalOffset: 10,
            rowHeaderWidth: 50,
            headerHeight: 40,
            rowHeight: 30);

        Assert.Equal(GridRegionKind.Cell, bounds.Region);
        Assert.Equal(140, bounds.Width);
        Assert.Equal(120 + 50 - 30, bounds.X);
        Assert.Equal(40 + (2 * 30) - 10, bounds.Y);
    }

    [Fact]
    public void HitTest_ReturnsRowHeader()
    {
        var columns = new GridColumnLayoutEngine(100);
        columns.EnsureColumnCount(2);
        var layout = new GridLayoutEngine(columns);
        var viewport = new GridViewportInfo(new GridVisibleRange(0, 10), new GridVisibleRange(0, 2), 0, 0);

        var hit = layout.HitTest(
            x: 20,
            y: 80,
            viewport,
            rowHeaderWidth: 40,
            headerHeight: 40,
            rowHeight: 20);

        Assert.Equal(GridRegionKind.RowHeader, hit.Region);
        Assert.Equal(2, hit.RowIndex);
    }

    [Fact]
    public void HitTest_ReturnsColumnHeader()
    {
        var columns = new GridColumnLayoutEngine(100);
        columns.EnsureColumnCount(3);
        columns.TrackWidth(0, 120);
        columns.TrackWidth(1, 140);
        columns.TrackWidth(2, 160);
        var layout = new GridLayoutEngine(columns);
        var viewport = new GridViewportInfo(new GridVisibleRange(0, 10), new GridVisibleRange(0, 3), 0, 0);

        var hit = layout.HitTest(
            x: 210,
            y: 20,
            viewport,
            rowHeaderWidth: 40,
            headerHeight: 40,
            rowHeight: 20);

        Assert.Equal(GridRegionKind.ColumnHeader, hit.Region);
        Assert.Equal(1, hit.ColumnIndex);
    }

    [Fact]
    public void HitTest_ReturnsCell()
    {
        var columns = new GridColumnLayoutEngine(100);
        columns.EnsureColumnCount(3);
        var layout = new GridLayoutEngine(columns);
        var viewport = new GridViewportInfo(new GridVisibleRange(0, 10), new GridVisibleRange(0, 3), 0, 0);

        var hit = layout.HitTest(
            x: 160,
            y: 95,
            viewport,
            rowHeaderWidth: 40,
            headerHeight: 40,
            rowHeight: 20);

        Assert.Equal(GridRegionKind.Cell, hit.Region);
        Assert.Equal(2, hit.RowIndex);
        Assert.Equal(1, hit.ColumnIndex);
    }
}
