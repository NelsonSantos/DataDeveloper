using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using DataDeveloper.Models;
using DataDeveloper.NextGrid;
using DataDeveloper.NextGrid.Editors;
using DataDeveloper.NextGrid.Renderers;
using DataDeveloper.NextGrid.UI;
using Xunit;

namespace DataDeveloper.Tests.NextGrid.UI;

public sealed class NextGridControlUiTests
{
    [AvaloniaFact]
    public void FirstClick_SelectsFirstCell()
    {
        var grid = CreateGrid(rowCount: 20, columnCount: 4);
        var window = CreateWindow(grid, 900, 420);

        window.Show();
        ExecuteLayout(window);

        var cell = grid.GetCellBoundsForTest(0, 0);
        var clickPoint = new Point(cell.X + Math.Min(12, cell.Width / 2), cell.Y + Math.Min(12, cell.Height / 2));
        grid.SelectCellAtLocalPointForTest(clickPoint);

        Assert.Equal(new GridCellAddress(0, 0), grid.GetFocusedCellForTest());
        window.Close();
    }

    [AvaloniaFact]
    public void FirstCellLocalHitTest_ReturnsFirstRow()
    {
        var grid = CreateGrid(rowCount: 20, columnCount: 4);
        var window = CreateWindow(grid, 900, 420);

        window.Show();
        ExecuteLayout(window);

        var cell = grid.GetCellBoundsForTest(0, 0);
        var clickPoint = new Point(cell.X + Math.Min(12, cell.Width / 2), cell.Y + 1);
        var hit = grid.HitTestAtLocalPointForTest(clickPoint);

        Assert.Equal(GridRegionKind.Cell, hit.Region);
        Assert.Equal(0, hit.RowIndex);
        Assert.Equal(0, hit.ColumnIndex);
        window.Close();
    }

    [AvaloniaFact]
    public void Resize_RecomputesVisibleRowCount()
    {
        var grid = CreateGrid(rowCount: 200, columnCount: 4);
        var window = CreateWindow(grid, 900, 520);

        window.Show();
        ExecuteLayout(window);
        var initialVisibleRows = grid.GetVisibleRowCountForTest();

        window.Height = 300;
        ExecuteLayout(window);
        var smallerVisibleRows = grid.GetVisibleRowCountForTest();

        window.Height = 680;
        ExecuteLayout(window);
        var largerVisibleRows = grid.GetVisibleRowCountForTest();

        Assert.True(smallerVisibleRows < initialVisibleRows);
        Assert.True(largerVisibleRows > smallerVisibleRows);
        window.Close();
    }

    [AvaloniaFact]
    public void DragSelection_CreatesRectangularRange()
    {
        var grid = CreateGrid(rowCount: 20, columnCount: 6);
        var window = CreateWindow(grid, 900, 420);

        window.Show();
        ExecuteLayout(window);

        grid.DragSelectCellsForTest(new GridCellAddress(1, 1), new GridCellAddress(3, 4));

        Assert.True(grid.SelectionContainsForTest(new GridCellAddress(1, 1)));
        Assert.True(grid.SelectionContainsForTest(new GridCellAddress(2, 3)));
        Assert.True(grid.SelectionContainsForTest(new GridCellAddress(3, 4)));
        Assert.False(grid.SelectionContainsForTest(new GridCellAddress(0, 0)));
        window.Close();
    }

    [AvaloniaFact]
    public void FirstClick_UpdatesSelectedRowIndex()
    {
        var grid = CreateGrid(rowCount: 20, columnCount: 4);
        var window = CreateWindow(grid, 900, 420);

        window.Show();
        ExecuteLayout(window);

        var cell = grid.GetCellBoundsForTest(3, 1);
        var clickPoint = new Point(cell.X + Math.Min(12, cell.Width / 2), cell.Y + Math.Min(12, cell.Height / 2));
        grid.SelectCellAtLocalPointForTest(clickPoint);

        Assert.Equal(3, grid.GetSelectedRowIndexForTest());
        window.Close();
    }

    [AvaloniaFact]
    public void InitialSelectionStatus_ShowsNothing()
    {
        var grid = CreateGrid(rowCount: 20, columnCount: 4);
        var window = CreateWindow(grid, 900, 420);

        window.Show();
        ExecuteLayout(window);

        Assert.Equal("Cell=nothing", grid.SelectionStatusText);
        window.Close();
    }

    [AvaloniaFact]
    public void FirstClick_UpdatesSelectionStatusWithCellCoordinate()
    {
        var grid = CreateGrid(rowCount: 20, columnCount: 4);
        var window = CreateWindow(grid, 900, 420);

        window.Show();
        ExecuteLayout(window);

        var cell = grid.GetCellBoundsForTest(2, 1);
        var clickPoint = new Point(cell.X + Math.Min(12, cell.Width / 2), cell.Y + Math.Min(12, cell.Height / 2));
        grid.SelectCellAtLocalPointForTest(clickPoint);

        Assert.Equal("Cell=3:2", grid.SelectionStatusText);
        window.Close();
    }

    [AvaloniaFact]
    public void DragSelection_UpdatesSelectionStatusWithSelectedSize()
    {
        var grid = CreateGrid(rowCount: 20, columnCount: 6);
        var window = CreateWindow(grid, 900, 420);

        window.Show();
        ExecuteLayout(window);

        grid.DragSelectCellsForTest(new GridCellAddress(1, 1), new GridCellAddress(3, 4));

        Assert.Equal("Selected=3 row(s) x 4 column(s)", grid.SelectionStatusText);
        window.Close();
    }

    [AvaloniaFact]
    public void BeginEditAndCommit_UpdatesCellValue()
    {
        var grid = CreateGrid(rowCount: 20, columnCount: 4);
        grid.CanEditCells = true;
        var window = CreateWindow(grid, 900, 420);

        window.Show();
        ExecuteLayout(window);

        var cell = grid.GetCellBoundsForTest(0, 0);
        var clickPoint = new Point(cell.X + Math.Min(12, cell.Width / 2), cell.Y + Math.Min(12, cell.Height / 2));
        grid.SelectCellAtLocalPointForTest(clickPoint);
        grid.BeginEditFocusedCellForTest();
        grid.CommitEditForTest("edited");

        Assert.Equal("edited", grid.Rows[0][0]);
        window.Close();
    }

    [AvaloniaFact]
    public void ActiveEditor_RepositionsWhenGridScrollsHorizontally()
    {
        var grid = CreateGrid(rowCount: 20, columnCount: 20);
        grid.CanEditCells = true;
        var window = CreateWindow(grid, 900, 420);

        window.Show();
        ExecuteLayout(window);

        var cell = grid.GetCellBoundsForTest(0, 1);
        var clickPoint = new Point(cell.X + Math.Min(12, cell.Width / 2), cell.Y + Math.Min(12, cell.Height / 2));
        grid.SelectCellAtLocalPointForTest(clickPoint);
        grid.BeginEditFocusedCellForTest();
        var initialPosition = grid.GetEditorPositionForTest();

        grid.ScrollToForTest(80, 0);
        ExecuteLayout(window);

        var scrolledBounds = grid.GetCellBoundsForTest(0, 1);
        Assert.True(grid.IsEditorVisibleForTest());
        Assert.NotEqual(initialPosition.X, grid.GetEditorPositionForTest().X);
        Assert.Equal(scrolledBounds.X + 1, grid.GetEditorPositionForTest().X, 3);
        Assert.Equal(scrolledBounds.Y + 1, grid.GetEditorPositionForTest().Y, 3);
        window.Close();
    }

    [AvaloniaFact]
    public void ActiveEditor_ClosesWhenCellScrollsOutOfView()
    {
        var grid = CreateGrid(rowCount: 20, columnCount: 20);
        grid.CanEditCells = true;
        var window = CreateWindow(grid, 400, 420);

        window.Show();
        ExecuteLayout(window);

        var originalValue = grid.Rows[0][0];
        var cell = grid.GetCellBoundsForTest(0, 0);
        var clickPoint = new Point(cell.X + Math.Min(12, cell.Width / 2), cell.Y + Math.Min(12, cell.Height / 2));
        grid.SelectCellAtLocalPointForTest(clickPoint);
        grid.BeginEditFocusedCellForTest();

        grid.ScrollToForTest(100000, 0);
        ExecuteLayout(window);

        Assert.False(grid.IsEditorVisibleForTest());
        Assert.Equal(originalValue, grid.Rows[0][0]);
        window.Close();
    }

    [AvaloniaFact]
    public void StructuredTextButtonHover_ShowsExplanatoryTooltip()
    {
        var grid = CreateStructuredTextGrid();
        var window = CreateWindow(grid, 900, 420);
        window.Show();
        ExecuteLayout(window);

        var jsonCellBounds = grid.GetCellBoundsForTest(0, 1);
        var iconCenter = new Point(jsonCellBounds.X + jsonCellBounds.Width - 12, jsonCellBounds.Y + (jsonCellBounds.Height / 2));
        var cellStart = new Point(jsonCellBounds.X + 2, jsonCellBounds.Y + 2);

        Assert.Equal("Open JSON viewer", grid.GetTooltipForPointForTest(iconCenter));
        Assert.Null(grid.GetTooltipForPointForTest(cellStart));
        window.Close();
    }

    [AvaloniaFact]
    public void StructuredTextButtonHit_OnlyDetectedWithinIconRegionOfCell()
    {
        var grid = CreateStructuredTextGrid();
        var window = CreateWindow(grid, 900, 420);

        window.Show();
        ExecuteLayout(window);

        var jsonCellBounds = grid.GetCellBoundsForTest(0, 1);
        var iconCenter = new Point(jsonCellBounds.X + jsonCellBounds.Width - 12, jsonCellBounds.Y + (jsonCellBounds.Height / 2));
        var cellStart = new Point(jsonCellBounds.X + 2, jsonCellBounds.Y + 2);

        Assert.True(grid.IsStructuredTextButtonHitForTest(0, 1, iconCenter));
        Assert.False(grid.IsStructuredTextButtonHitForTest(0, 1, cellStart));
        window.Close();
    }

    [AvaloniaFact]
    public void StructuredTextButtonHit_AlsoAppearsOnPlainTextColumns()
    {
        // Every text column gets the expand button now; JSON/XML detection happens only
        // when the button is actually clicked, not while deciding whether to draw it.
        var grid = CreateStructuredTextGrid();
        var window = CreateWindow(grid, 900, 420);

        window.Show();
        ExecuteLayout(window);

        var plainCellBounds = grid.GetCellBoundsForTest(0, 0);
        var plainIconArea = new Point(plainCellBounds.X + plainCellBounds.Width - 12, plainCellBounds.Y + (plainCellBounds.Height / 2));
        Assert.True(grid.IsStructuredTextButtonHitForTest(0, 0, plainIconArea));
        window.Close();
    }

    [AvaloniaFact]
    public void RowsReplacedAfterInitialLayout_RaisesScrollInvalidatedOnceWidthsAreRecomputed()
    {
        var headers = new ObservableCollection<string> { "Field1", "Field2", "Field3" };
        var types = new ObservableCollection<Type> { typeof(string), typeof(string), typeof(string) };
        var rows = new ObservableCollection<IReadOnlyList<object?>> { new object?[] { "short", "short", "short" } };

        var grid = new NextGridControl { Headers = headers, ColumnTypes = types, Rows = rows };
        var window = CreateWindow(grid, 300, 200);
        window.Show();
        ExecuteLayout(window);

        var extentSnapshots = new List<double>();
        grid.SubscribeScrollInvalidatedForTest(() => extentSnapshots.Add(grid.GetExtentWidthForTest()));

        var longValue = new string('x', 200);
        rows.Clear();
        rows.Add(new object?[] { longValue, longValue, longValue });
        ExecuteLayout(window);

        var finalExtentWidth = grid.GetExtentWidthForTest();
        Assert.True(finalExtentWidth > 300);
        Assert.Contains(extentSnapshots, w => Math.Abs(w - finalExtentWidth) < 0.5);
        window.Close();
    }

    [AvaloniaFact]
    public void StructuredTextButtonRect_ClampsToAvoidVerticalScrollBarReserve()
    {
        var grid = CreateStructuredTextGrid();
        var window = CreateWindow(grid, 900, 420);
        window.Show();
        ExecuteLayout(window);

        var bounds = grid.GetCellBoundsForTest(0, 1);
        var naturalIconCenter = new Point(bounds.X + bounds.Width - 12, bounds.Y + (bounds.Height / 2));

        grid.SetScrollBarReserveForTest(0, 900);

        Assert.False(grid.IsStructuredTextButtonHitForTest(0, 1, naturalIconCenter));
        Assert.True(grid.IsStructuredTextButtonHitForTest(0, 1, new Point(bounds.X + 2, bounds.Y + (bounds.Height / 2))));
        window.Close();
    }

    [AvaloniaFact]
    public void StructuredTextButtonRect_StaysAnchoredToCellEndWhenColumnIsWiderThanViewport()
    {
        var longJson = "{\"a\":\"" + new string('x', 400) + "\"}";
        var headers = new ObservableCollection<string> { "Payload" };
        var types = new ObservableCollection<Type> { typeof(string) };
        var rows = new ObservableCollection<IReadOnlyList<object?>>
        {
            new EditableGridRow([longJson])
        };
        var grid = new NextGridControl { Headers = headers, ColumnTypes = types, Rows = rows };
        var window = CreateWindow(grid, 300, 200);
        window.Show();
        ExecuteLayout(window);

        // Auto-width caps the column below the viewport width on first display; widen it
        // manually (as a user drag-resize would) to exercise the overflowing-column scenario.
        grid.SetColumnWidthForTest(0, 2000);
        ExecuteLayout(window);

        var bounds = grid.GetCellBoundsForTest(0, 0);
        Assert.True(bounds.X + bounds.Width > 300);

        grid.SetScrollBarReserveForTest(0, 20);

        var naturalRect = TextGridCellRenderer.GetIconRect(bounds);
        var actualRect = grid.GetStructuredTextButtonRectForTest(0, 0);

        Assert.Equal(naturalRect.X, actualRect.X, 3);
        window.Close();
    }

    [AvaloniaFact]
    public void LastColumnResizeHandle_IsReachableAtMaximumScroll()
    {
        var longJson = "{\"a\":\"" + new string('x', 400) + "\"}";
        var headers = new ObservableCollection<string> { "Payload" };
        var types = new ObservableCollection<Type> { typeof(string) };
        var rows = new ObservableCollection<IReadOnlyList<object?>>
        {
            new EditableGridRow([longJson])
        };
        var grid = new NextGridControl { Headers = headers, ColumnTypes = types, Rows = rows };
        var window = CreateWindow(grid, 300, 200);
        window.Show();
        ExecuteLayout(window);

        grid.SetColumnWidthForTest(0, 2000);
        ExecuteLayout(window);

        grid.SetScrollBarReserveForTest(0, 20);
        grid.ScrollToForTest(1_000_000, 0);
        ExecuteLayout(window);

        var bounds = grid.GetCellBoundsForTest(0, 0);
        var reachableRight = grid.GetViewportWidthForTest() - 20;
        Assert.True(
            bounds.X + bounds.Width <= reachableRight + 0.5,
            $"Column boundary at {bounds.X + bounds.Width} is not within the reachable area (<= {reachableRight}); the scrollbar would intercept the resize handle.");

        var resizePoint = new Point(bounds.X + bounds.Width, 5);
        Assert.True(grid.TryGetResizeColumnIndexForTest(resizePoint, out var columnIndex));
        Assert.Equal(0, columnIndex);
        window.Close();
    }

    [AvaloniaFact]
    public void LastColumnBoundary_AlreadyReachableAtMaximumScrollWithoutScrollBarReserve()
    {
        var longJson = "{\"a\":\"" + new string('x', 400) + "\"}";
        var headers = new ObservableCollection<string> { "Payload" };
        var types = new ObservableCollection<Type> { typeof(string) };
        var rows = new ObservableCollection<IReadOnlyList<object?>>
        {
            new EditableGridRow([longJson])
        };
        var grid = new NextGridControl { Headers = headers, ColumnTypes = types, Rows = rows };
        var window = CreateWindow(grid, 300, 200);
        window.Show();
        ExecuteLayout(window);

        grid.ScrollToForTest(1_000_000, 0);
        ExecuteLayout(window);

        var bounds = grid.GetCellBoundsForTest(0, 0);
        Assert.True(bounds.X + bounds.Width <= grid.GetViewportWidthForTest() + 0.5);
        window.Close();
    }

    [AvaloniaFact]
    public void AutoWidth_CapsInitialColumnWidthAtHalfTheGridWidth()
    {
        var longJson = "{\"a\":\"" + new string('x', 400) + "\"}";
        var headers = new ObservableCollection<string> { "Payload" };
        var types = new ObservableCollection<Type> { typeof(string) };
        var rows = new ObservableCollection<IReadOnlyList<object?>>
        {
            new EditableGridRow([longJson])
        };
        var grid = new NextGridControl { Headers = headers, ColumnTypes = types, Rows = rows };
        var window = CreateWindow(grid, 900, 200);
        window.Show();
        ExecuteLayout(window);

        var bounds = grid.GetCellBoundsForTest(0, 0);
        Assert.True(bounds.Width <= (grid.GetViewportWidthForTest() * 0.5) + 0.5);

        grid.SetColumnWidthForTest(0, 2000);
        ExecuteLayout(window);

        var widenedBounds = grid.GetCellBoundsForTest(0, 0);
        Assert.Equal(2000, widenedBounds.Width, 3);
        window.Close();
    }

    [AvaloniaFact]
    public void StructuredTextCellView_RequestedWithEditableTrueForEditableColumn()
    {
        var grid = CreateStructuredTextGrid();
        grid.CanEditCells = true;
        var window = CreateWindow(grid, 900, 420);
        window.Show();
        ExecuteLayout(window);

        GridStructuredTextCellViewRequestedEventArgs? received = null;
        grid.StructuredTextCellViewRequested += (_, args) => received = args;

        grid.RequestStructuredTextCellViewForTest(new GridCellAddress(0, 1));

        Assert.NotNull(received);
        Assert.Equal(new GridCellAddress(0, 1), received!.Cell);
        Assert.Equal("{\"a\":1}", received.Value);
        Assert.True(received.IsEditable);
        Assert.Equal(StructuredTextKind.Json, received.Kind);
        window.Close();
    }

    [AvaloniaFact]
    public void StructuredTextCellView_RequestedWithXmlValue_ReportsXmlKind()
    {
        var headers = new ObservableCollection<string> { "Name", "Payload" };
        var types = new ObservableCollection<Type> { typeof(string), typeof(string) };
        var rows = new ObservableCollection<IReadOnlyList<object?>>
        {
            new EditableGridRow(["Alice", "<root><a>1</a></root>"])
        };
        var grid = new NextGridControl { Headers = headers, ColumnTypes = types, Rows = rows, CanEditCells = true };
        var window = CreateWindow(grid, 900, 420);
        window.Show();
        ExecuteLayout(window);

        GridStructuredTextCellViewRequestedEventArgs? received = null;
        grid.StructuredTextCellViewRequested += (_, args) => received = args;

        grid.RequestStructuredTextCellViewForTest(new GridCellAddress(0, 1));

        Assert.NotNull(received);
        Assert.Equal("<root><a>1</a></root>", received!.Value);
        Assert.Equal(StructuredTextKind.Xml, received.Kind);
        window.Close();
    }

    [AvaloniaFact]
    public void StructuredTextCellView_RequestedWithEditableFalseWhenGridNotEditable()
    {
        var grid = CreateStructuredTextGrid();
        grid.CanEditCells = false;
        var window = CreateWindow(grid, 900, 420);
        window.Show();
        ExecuteLayout(window);

        GridStructuredTextCellViewRequestedEventArgs? received = null;
        grid.StructuredTextCellViewRequested += (_, args) => received = args;

        grid.RequestStructuredTextCellViewForTest(new GridCellAddress(0, 1));

        Assert.NotNull(received);
        Assert.False(received!.IsEditable);
        window.Close();
    }

    [AvaloniaFact]
    public void StructuredTextCellView_RequestedWithEditableFalseWhenColumnIsReadOnly()
    {
        var grid = CreateStructuredTextGrid();
        grid.CanEditCells = true;
        grid.ReadOnlyColumns = new ObservableCollection<int> { 1 };
        var window = CreateWindow(grid, 900, 420);
        window.Show();
        ExecuteLayout(window);

        GridStructuredTextCellViewRequestedEventArgs? received = null;
        grid.StructuredTextCellViewRequested += (_, args) => received = args;

        grid.RequestStructuredTextCellViewForTest(new GridCellAddress(0, 1));

        Assert.NotNull(received);
        Assert.False(received!.IsEditable);
        window.Close();
    }

    [AvaloniaFact]
    public void CommitStructuredTextCellEdit_UpdatesRowValueAndRaisesCellEditCommitted()
    {
        var grid = CreateStructuredTextGrid();
        grid.CanEditCells = true;
        var window = CreateWindow(grid, 900, 420);
        window.Show();
        ExecuteLayout(window);

        var committed = false;
        grid.CellEditCommitted += (_, _) => committed = true;

        grid.RequestStructuredTextCellViewForTest(new GridCellAddress(0, 1));
        grid.CommitStructuredTextCellEdit("{\"a\":2}");

        Assert.True(committed);
        Assert.Equal("{\"a\":2}", grid.Rows[0][1]);
        window.Close();
    }

    [AvaloniaFact]
    public void CancelStructuredTextCellEdit_LeavesRowValueUnchanged()
    {
        var grid = CreateStructuredTextGrid();
        grid.CanEditCells = true;
        var window = CreateWindow(grid, 900, 420);
        window.Show();
        ExecuteLayout(window);

        grid.RequestStructuredTextCellViewForTest(new GridCellAddress(0, 1));
        grid.CancelStructuredTextCellEdit();

        Assert.Equal("{\"a\":1}", grid.Rows[0][1]);
        window.Close();
    }

    private static NextGridControl CreateStructuredTextGrid()
    {
        var headers = new ObservableCollection<string> { "Name", "Payload" };
        var types = new ObservableCollection<Type> { typeof(string), typeof(string) };
        var rows = new ObservableCollection<IReadOnlyList<object?>>
        {
            new EditableGridRow(["Alice", "{\"a\":1}"])
        };

        return new NextGridControl
        {
            Headers = headers,
            ColumnTypes = types,
            Rows = rows
        };
    }

    [AvaloniaFact]
    public void ReadOnlyColumn_RemainsLockedForExistingRow_ButAllowsNewRow()
    {
        var headers = new ObservableCollection<string> { "Id", "Name" };
        var types = new ObservableCollection<Type> { typeof(string), typeof(string) };
        var rows = new ObservableCollection<IReadOnlyList<object?>>
        {
            new EditableGridRow(["1", "Alice"]),
            new EditableGridRow([null, null], isNew: true)
        };

        var grid = new NextGridControl
        {
            Headers = headers,
            ColumnTypes = types,
            Rows = rows,
            CanEditCells = true,
            ReadOnlyColumns = new ObservableCollection<int> { 0 }
        };
        var window = CreateWindow(grid, 900, 420);

        window.Show();
        ExecuteLayout(window);

        var existingCell = grid.GetCellBoundsForTest(0, 0);
        grid.SelectCellAtLocalPointForTest(new Point(existingCell.X + 8, existingCell.Y + 8));
        grid.BeginEditFocusedCellForTest();
        grid.CommitEditForTest("2");

        var newCell = grid.GetCellBoundsForTest(1, 0);
        grid.SelectCellAtLocalPointForTest(new Point(newCell.X + 8, newCell.Y + 8));
        grid.BeginEditFocusedCellForTest();
        grid.CommitEditForTest("10");

        Assert.Equal("1", grid.Rows[0][0]);
        Assert.Equal("10", grid.Rows[1][0]);
        window.Close();
    }

    [AvaloniaFact]
    public void BooleanCell_TogglesValueOnEdit()
    {
        var grid = new NextGridControl
        {
            Headers = new ObservableCollection<string> { "Enabled" },
            ColumnTypes = new ObservableCollection<Type> { typeof(bool) },
            Rows = new ObservableCollection<IReadOnlyList<object?>> { new object?[] { false } },
            CanEditCells = true
        };
        var window = CreateWindow(grid, 400, 200);

        window.Show();
        ExecuteLayout(window);

        var cell = grid.GetCellBoundsForTest(0, 0);
        grid.SelectCellAtLocalPointForTest(new Point(cell.X + 8, cell.Y + 8));
        grid.BeginEditFocusedCellForTest();
        grid.CommitBooleanEditForTest(true);

        Assert.Equal(true, grid.Rows[0][0]);
        window.Close();
    }

    [AvaloniaFact]
    public void NullableBooleanCell_AllowsNullSelection()
    {
        var grid = new NextGridControl
        {
            Headers = new ObservableCollection<string> { "Enabled" },
            ColumnTypes = new ObservableCollection<Type> { typeof(bool?) },
            Rows = new ObservableCollection<IReadOnlyList<object?>> { new object?[] { true } },
            CanEditCells = true
        };
        var window = CreateWindow(grid, 400, 200);

        window.Show();
        ExecuteLayout(window);

        var cell = grid.GetCellBoundsForTest(0, 0);
        grid.SelectCellAtLocalPointForTest(new Point(cell.X + 8, cell.Y + 8));
        grid.BeginEditFocusedCellForTest();
        grid.CommitBooleanEditForTest(null);

        Assert.Null(grid.Rows[0][0]);
        window.Close();
    }

    [AvaloniaFact]
    public void DateTimeCell_UsesPopupAndCommitsSelection()
    {
        var grid = new NextGridControl
        {
            Headers = new ObservableCollection<string> { "CreatedAt" },
            ColumnTypes = new ObservableCollection<Type> { typeof(DateTime) },
            Rows = new ObservableCollection<IReadOnlyList<object?>> { new object?[] { new DateTime(2026, 4, 4, 10, 0, 0) } },
            CanEditCells = true
        };
        var window = CreateWindow(grid, 400, 200);

        window.Show();
        ExecuteLayout(window);

        var cell = grid.GetCellBoundsForTest(0, 0);
        grid.SelectCellAtLocalPointForTest(new Point(cell.X + 8, cell.Y + 8));
        grid.BeginEditFocusedCellForTest();
        grid.CommitDateTimeEditForTest(new DateTime(2026, 4, 5, 14, 30, 45));

        Assert.Equal(new DateTime(2026, 4, 5, 14, 30, 45), grid.Rows[0][0]);
        window.Close();
    }

    [Fact]
    public void DateTimePopup_UsesOriginalCellValueWhenSessionCurrentValueIsText()
    {
        var session = new GridEditSession(
            new GridCellAddress(0, 0),
            typeof(DateTime),
            new DateTime(2026, 4, 6, 20, 7, 14),
            "2026-04-06 20:07:14");

        var initial = NextGridControl.ResolveDateTimePopupInitialValue(session);

        Assert.Equal(new DateTimeOffset(new DateTime(2026, 4, 6, 20, 7, 14)), initial);
    }

    [AvaloniaFact]
    public void CanCopy_ReflectsWhetherACellIsSelected()
    {
        var grid = CreateGrid(rowCount: 5, columnCount: 3);
        var window = CreateWindow(grid, 900, 420);

        window.Show();
        ExecuteLayout(window);

        Assert.False(grid.CanCopy);

        var cell = grid.GetCellBoundsForTest(0, 0);
        grid.SelectCellAtLocalPointForTest(new Point(cell.X + 8, cell.Y + 8));

        Assert.True(grid.CanCopy);
        window.Close();
    }

    [AvaloniaFact]
    public async Task CopySelectionAsync_CopiesFocusedCellValueToClipboard()
    {
        var grid = CreateGrid(rowCount: 5, columnCount: 3);
        var window = CreateWindow(grid, 900, 420);

        window.Show();
        ExecuteLayout(window);

        var cell = grid.GetCellBoundsForTest(2, 1);
        grid.SelectCellAtLocalPointForTest(new Point(cell.X + 8, cell.Y + 8));

        await grid.CopySelectionAsync();

        var text = await window.Clipboard!.GetTextAsync();
        Assert.Equal("R3C2", text);
        window.Close();
    }

    [AvaloniaFact]
    public async Task CtrlOrCmdC_WithFocusedCell_CopiesCellValueToClipboard()
    {
        var grid = CreateGrid(rowCount: 5, columnCount: 3);
        var window = CreateWindow(grid, 900, 420);

        window.Show();
        ExecuteLayout(window);

        var cell = grid.GetCellBoundsForTest(1, 1);
        var clickPoint = new Point(cell.X + 8, cell.Y + 8);
        window.MouseDown(clickPoint, MouseButton.Left);
        window.MouseUp(clickPoint, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        window.KeyPressQwerty(PhysicalKey.C, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();

        var text = await window.Clipboard!.GetTextAsync();
        Assert.Equal("R2C2", text);
        window.Close();
    }

    private static NextGridControl CreateGrid(int rowCount, int columnCount)
    {
        var headers = new ObservableCollection<string>();
        var types = new ObservableCollection<Type>();
        var rows = new ObservableCollection<IReadOnlyList<object?>>();

        for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
        {
            headers.Add($"Field{columnIndex + 1}");
            types.Add(typeof(string));
        }

        for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
        {
            var row = new object?[columnCount];
            for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
                row[columnIndex] = $"R{rowIndex + 1}C{columnIndex + 1}";

            rows.Add(row);
        }

        return new NextGridControl
        {
            Headers = headers,
            ColumnTypes = types,
            Rows = rows
        };
    }

    private static Window CreateWindow(Control content, double width, double height)
    {
        return new Window
        {
            Width = width,
            Height = height,
            Content = content
        };
    }

    private static void ExecuteLayout(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        var size = new Size(window.Width, window.Height);
        window.Measure(size);
        window.Arrange(new Rect(size));
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }
}
