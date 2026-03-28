using DataDeveloper.NextGrid;
using Xunit;

namespace DataDeveloper.Tests.NextGrid;

public sealed class GridColumnLayoutEngineTests
{
    [Fact]
    public void EnsureColumnCount_InitializesMinimumWidths()
    {
        var engine = new GridColumnLayoutEngine(120);

        engine.EnsureColumnCount(3);

        Assert.Equal([120d, 120d, 120d], engine.Widths);
    }

    [Fact]
    public void TrackWidth_GrowsOnlyWhenMeasuredWidthIsLarger()
    {
        var engine = new GridColumnLayoutEngine(120);
        engine.EnsureColumnCount(1);

        var changed = engine.TrackWidth(0, 240);
        var changedAgain = engine.TrackWidth(0, 180);

        Assert.True(changed);
        Assert.False(changedAgain);
        Assert.Equal(240, engine.Widths[0]);
    }

    [Fact]
    public void SetWidth_ChangesWidthAndHonorsMinimum()
    {
        var engine = new GridColumnLayoutEngine(120);
        engine.EnsureColumnCount(1);

        var changed = engine.SetWidth(0, 260);
        var changedAgain = engine.SetWidth(0, 10);

        Assert.True(changed);
        Assert.True(changedAgain);
        Assert.Equal(120, engine.Widths[0]);
    }

    [Fact]
    public void GetColumnIndexAt_UsesAccumulatedWidths()
    {
        var engine = new GridColumnLayoutEngine(100);
        engine.EnsureColumnCount(3);
        engine.TrackWidth(0, 140);
        engine.TrackWidth(1, 220);
        engine.TrackWidth(2, 180);

        Assert.Equal(0, engine.GetColumnIndexAt(0));
        Assert.Equal(1, engine.GetColumnIndexAt(150));
        Assert.Equal(2, engine.GetColumnIndexAt(380));
    }

    [Fact]
    public void GetHorizontalOffsetForCell_DoesNotScrollWhenCellIsVisible()
    {
        var engine = new GridColumnLayoutEngine(100);
        engine.EnsureColumnCount(4);
        engine.TrackWidth(0, 120);
        engine.TrackWidth(1, 140);
        engine.TrackWidth(2, 160);
        engine.TrackWidth(3, 180);

        var offset = engine.GetHorizontalOffsetForCell(
            currentOffset: 100,
            viewportContentWidth: 400,
            columnIndex: 2,
            movedLeft: false,
            movedRight: true);

        Assert.Equal(100, offset);
    }

    [Fact]
    public void GetHorizontalOffsetForCell_ScrollsMinimumForRegularColumn()
    {
        var engine = new GridColumnLayoutEngine(100);
        engine.EnsureColumnCount(5);
        engine.TrackWidth(0, 120);
        engine.TrackWidth(1, 140);
        engine.TrackWidth(2, 160);
        engine.TrackWidth(3, 180);
        engine.TrackWidth(4, 200);

        var offset = engine.GetHorizontalOffsetForCell(
            currentOffset: 0,
            viewportContentWidth: 300,
            columnIndex: 3,
            movedLeft: false,
            movedRight: true);

        Assert.Equal(300, offset);
    }

    [Fact]
    public void GetHorizontalOffsetForCell_AnchorsWideColumnAtStartWhenMovingRight()
    {
        var engine = new GridColumnLayoutEngine(100);
        engine.EnsureColumnCount(3);
        engine.TrackWidth(0, 120);
        engine.TrackWidth(1, 700);
        engine.TrackWidth(2, 120);

        var offset = engine.GetHorizontalOffsetForCell(
            currentOffset: 0,
            viewportContentWidth: 300,
            columnIndex: 1,
            movedLeft: false,
            movedRight: true);

        Assert.Equal(120, offset);
    }

    [Fact]
    public void GetHorizontalOffsetForCell_ScrollsLeftWhenTargetCellIsPartiallyHidden()
    {
        var engine = new GridColumnLayoutEngine(100);
        engine.EnsureColumnCount(4);
        engine.SetWidth(0, 120);
        engine.SetWidth(1, 140);
        engine.SetWidth(2, 160);
        engine.SetWidth(3, 180);

        var offset = engine.GetHorizontalOffsetForCell(
            currentOffset: 150,
            viewportContentWidth: 300,
            columnIndex: 1,
            movedLeft: true,
            movedRight: false);

        Assert.Equal(120, offset);
    }
}
