using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using DataDeveloper.NextGrid;
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
