using Avalonia.Input;
using DataDeveloper.NextGrid;
using Xunit;

namespace DataDeveloper.Tests.NextGrid.Controller;

public sealed class GridTableControllerTests
{
    [Fact]
    public void HandlePointerSelection_SelectsRowFromRowHeader()
    {
        var columns = new GridColumnLayoutEngine(100);
        columns.EnsureColumnCount(4);
        var selection = new GridSelectionModel();
        var controller = new GridTableController(columns, new GridLayoutEngine(columns), new GridViewportEngine(columns), selection);
        controller.SetDimensions(10, 4);
        controller.UpdateViewport(new GridViewportState(0, 0, 600, 200, 40, 40, 20));

        controller.HandlePointerSelection(new GridHitTestResult(GridRegionKind.RowHeader, 3, -1), KeyModifiers.None);

        Assert.True(selection.Contains(new GridCellAddress(3, 0)));
        Assert.True(selection.Contains(new GridCellAddress(3, 3)));
    }

    [Fact]
    public void HandlePointerSelection_ExtendsColumnRangeWithShift()
    {
        var columns = new GridColumnLayoutEngine(100);
        columns.EnsureColumnCount(5);
        var selection = new GridSelectionModel();
        var controller = new GridTableController(columns, new GridLayoutEngine(columns), new GridViewportEngine(columns), selection);
        controller.SetDimensions(3, 5);
        controller.UpdateViewport(new GridViewportState(0, 0, 600, 200, 40, 40, 20));

        controller.HandlePointerSelection(new GridHitTestResult(GridRegionKind.ColumnHeader, -1, 1), KeyModifiers.None);
        controller.HandlePointerSelection(new GridHitTestResult(GridRegionKind.ColumnHeader, -1, 3), KeyModifiers.Shift);

        Assert.True(selection.Contains(new GridCellAddress(0, 1)));
        Assert.True(selection.Contains(new GridCellAddress(2, 2)));
        Assert.True(selection.Contains(new GridCellAddress(1, 3)));
    }

    [Fact]
    public void MoveFocus_UpdatesViewportOffsets()
    {
        var columns = new GridColumnLayoutEngine(100);
        columns.EnsureColumnCount(5);
        columns.TrackWidth(0, 120);
        columns.TrackWidth(1, 140);
        columns.TrackWidth(2, 160);
        columns.TrackWidth(3, 180);
        columns.TrackWidth(4, 200);
        var selection = new GridSelectionModel();
        var controller = new GridTableController(columns, new GridLayoutEngine(columns), new GridViewportEngine(columns), selection);
        controller.SetDimensions(100, 5);
        controller.UpdateViewport(new GridViewportState(0, 0, 340, 160, 40, 40, 30));
        selection.SelectCell(new GridCellAddress(5, 2));

        var result = controller.MoveFocus(GridNavigationDirection.Right);

        Assert.Equal(new GridCellAddress(5, 3), result.Cell);
        Assert.Equal(300, result.HorizontalOffset);
    }

    [Fact]
    public void State_ExposesTopRowIndexAndVisibleRowCount()
    {
        var columns = new GridColumnLayoutEngine(100);
        columns.EnsureColumnCount(3);
        var controller = new GridTableController(columns, new GridLayoutEngine(columns), new GridViewportEngine(columns), new GridSelectionModel());
        controller.SetDimensions(50, 3);
        controller.UpdateViewport(new GridViewportState(0, 84, 320, 174, 40, 34, 28));

        var state = controller.State;

        Assert.Equal(3, state.TopRowIndex);
        Assert.Equal(5, state.VisibleRowCount);
        Assert.Equal(50, state.RowCount);
        Assert.Equal(3, state.ColumnCount);
    }

    [Fact]
    public void GetCellBounds_UsesViewportOffsets()
    {
        var columns = new GridColumnLayoutEngine(100);
        columns.EnsureColumnCount(3);
        columns.TrackWidth(0, 120);
        columns.TrackWidth(1, 140);
        columns.TrackWidth(2, 160);
        var controller = new GridTableController(columns, new GridLayoutEngine(columns), new GridViewportEngine(columns), new GridSelectionModel());
        controller.SetDimensions(20, 3);
        controller.UpdateViewport(new GridViewportState(120, 56, 320, 200, 44, 34, 28));

        var bounds = controller.GetCellBounds(4, 1);

        Assert.Equal(44, bounds.X);
        Assert.Equal(90, bounds.Y);
        Assert.Equal(140, bounds.Width);
        Assert.Equal(28, bounds.Height);
    }

    [Fact]
    public void HitTest_ReturnsFirstCellForPointInsideFirstVisibleCell()
    {
        var columns = new GridColumnLayoutEngine(100);
        columns.EnsureColumnCount(3);
        columns.SetWidth(0, 120);
        columns.SetWidth(1, 140);
        var controller = new GridTableController(columns, new GridLayoutEngine(columns), new GridViewportEngine(columns), new GridSelectionModel());
        controller.SetDimensions(20, 3);
        controller.UpdateViewport(new GridViewportState(0, 0, 320, 200, 44, 34, 28));

        var hit = controller.HitTest(60, 50);

        Assert.Equal(GridRegionKind.Cell, hit.Region);
        Assert.Equal(0, hit.RowIndex);
        Assert.Equal(0, hit.ColumnIndex);
    }

    [Fact]
    public void HitTest_ReturnsFirstRowForPointNearTopOfFirstCell()
    {
        var columns = new GridColumnLayoutEngine(100);
        columns.EnsureColumnCount(3);
        columns.SetWidth(0, 120);
        var controller = new GridTableController(columns, new GridLayoutEngine(columns), new GridViewportEngine(columns), new GridSelectionModel());
        controller.SetDimensions(20, 3);
        controller.UpdateViewport(new GridViewportState(0, 0, 320, 200, 44, 34, 28));

        var bounds = controller.GetCellBounds(0, 0);
        var hit = controller.HitTest(bounds.X + 12, bounds.Y + 1);

        Assert.Equal(GridRegionKind.Cell, hit.Region);
        Assert.Equal(0, hit.RowIndex);
        Assert.Equal(0, hit.ColumnIndex);
    }

    [Fact]
    public void EnsureVisible_ScrollsDownUsingVisibleRowCount()
    {
        var columns = new GridColumnLayoutEngine(100);
        columns.EnsureColumnCount(3);
        var controller = new GridTableController(columns, new GridLayoutEngine(columns), new GridViewportEngine(columns), new GridSelectionModel());
        controller.SetDimensions(100, 3);
        controller.UpdateViewport(new GridViewportState(0, 0, 320, 174, 40, 34, 28));

        var result = controller.EnsureVisible(new GridCellAddress(6, 1), GridNavigationDirection.Down);

        Assert.Equal(56, result.VerticalOffset);
    }

    [Fact]
    public void EnsureVisible_DoesNotScrollWhenRowIsAlreadyVisible()
    {
        var columns = new GridColumnLayoutEngine(100);
        columns.EnsureColumnCount(3);
        var controller = new GridTableController(columns, new GridLayoutEngine(columns), new GridViewportEngine(columns), new GridSelectionModel());
        controller.SetDimensions(100, 3);
        controller.UpdateViewport(new GridViewportState(0, 56, 320, 174, 40, 34, 28));

        var result = controller.EnsureVisible(new GridCellAddress(3, 1), GridNavigationDirection.Up);

        Assert.Equal(56, result.VerticalOffset);
    }

    [Fact]
    public void GetRowRenderRange_UsesTopRowIndexAndVisibleRowCount()
    {
        var columns = new GridColumnLayoutEngine(100);
        columns.EnsureColumnCount(3);
        var controller = new GridTableController(columns, new GridLayoutEngine(columns), new GridViewportEngine(columns), new GridSelectionModel());
        controller.SetDimensions(100, 3);
        controller.UpdateViewport(new GridViewportState(0, 84, 320, 174, 40, 34, 28));

        var range = controller.GetRowRenderRange();

        Assert.Equal(2, range.Start);
        Assert.Equal(10, range.EndExclusive);
    }

    [Fact]
    public void GetRowRenderRange_IncludesLastPartiallyVisibleRow()
    {
        var columns = new GridColumnLayoutEngine(100);
        columns.EnsureColumnCount(3);
        var controller = new GridTableController(columns, new GridLayoutEngine(columns), new GridViewportEngine(columns), new GridSelectionModel());
        controller.SetDimensions(100, 3);
        controller.UpdateViewport(new GridViewportState(0, 0, 320, 267, 44, 34, 28));

        var range = controller.GetRowRenderRange();

        Assert.Equal(0, range.Start);
        Assert.True(range.EndExclusive >= 10);
    }

    [Fact]
    public void ExtendFocus_WithPageDown_ExtendsSelectionByVisibleRows()
    {
        var columns = new GridColumnLayoutEngine(100);
        columns.EnsureColumnCount(3);
        var selection = new GridSelectionModel();
        var controller = new GridTableController(columns, new GridLayoutEngine(columns), new GridViewportEngine(columns), selection);
        controller.SetDimensions(100, 3);
        controller.UpdateViewport(new GridViewportState(0, 0, 320, 174, 40, 34, 28));
        selection.SelectCell(new GridCellAddress(0, 1));

        var result = controller.ExtendFocus(GridNavigationDirection.PageDown, 5);

        Assert.Equal(new GridCellAddress(5, 1), result.Cell);
        Assert.True(selection.Contains(new GridCellAddress(0, 1)));
        Assert.True(selection.Contains(new GridCellAddress(5, 1)));
    }
}
