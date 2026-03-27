using DataDeveloper.NextGrid;
using Xunit;

namespace DataDeveloper.Tests.NextGrid;

public sealed class GridViewportEngineTests
{
    [Fact]
    public void GetVisibleRows_UsesOffsetViewportAndOverscan()
    {
        var columns = new GridColumnLayoutEngine(100);
        var viewport = new GridViewportEngine(columns);
        var state = new GridViewportState(0, 130, 600, 160, 40, 40, 30);

        var range = viewport.GetVisibleRows(state, rowCount: 100);

        Assert.Equal(3, range.Start);
        Assert.Equal(10, range.EndExclusive);
    }

    [Fact]
    public void GetVisibleColumns_UsesColumnWidths()
    {
        var columns = new GridColumnLayoutEngine(100);
        columns.EnsureColumnCount(4);
        columns.TrackWidth(0, 120);
        columns.TrackWidth(1, 140);
        columns.TrackWidth(2, 180);
        columns.TrackWidth(3, 160);
        var viewport = new GridViewportEngine(columns);
        var state = new GridViewportState(150, 0, 320, 200, 60, 40, 30);

        var range = viewport.GetVisibleColumns(state);

        Assert.Equal(0, range.Start);
        Assert.Equal(4, range.EndExclusive);
    }

}
