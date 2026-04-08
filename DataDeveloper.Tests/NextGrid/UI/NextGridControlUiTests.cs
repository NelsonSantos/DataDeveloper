using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using DataDeveloper.Models;
using DataDeveloper.NextGrid;
using DataDeveloper.NextGrid.Editors;
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
