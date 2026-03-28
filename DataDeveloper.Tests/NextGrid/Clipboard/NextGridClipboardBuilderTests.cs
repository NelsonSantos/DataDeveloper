using System.Collections.ObjectModel;
using DataDeveloper.NextGrid;
using DataDeveloper.NextGrid.Clipboard;
using DataDeveloper.NextGrid.Renderers;
using Xunit;

namespace DataDeveloper.Tests.NextGrid.Clipboard;

public sealed class NextGridClipboardBuilderTests
{
    [Fact]
    public void Build_RectangularRange_ReturnsTabDelimitedText()
    {
        var builder = new NextGridClipboardBuilder(new GridRendererRegistry());
        var rows = new ObservableCollection<IReadOnlyList<object?>>
        {
            new object?[] { "A1", "B1", "C1" },
            new object?[] { "A2", "B2", "C2" },
            new object?[] { "A3", "B3", "C3" }
        };
        var columnTypes = new ObservableCollection<Type> { typeof(string), typeof(string), typeof(string) };

        var text = builder.Build(
            [new GridSelectionRange(new GridCellAddress(0, 1), new GridCellAddress(1, 2))],
            rows,
            columnTypes);

        Assert.Equal("B1\tC1\nB2\tC2", text);
    }

    [Fact]
    public void Build_ColumnSelection_UsesLoadedRowsOnly()
    {
        var builder = new NextGridClipboardBuilder(new GridRendererRegistry());
        var rows = new ObservableCollection<IReadOnlyList<object?>>
        {
            new object?[] { 1, "A1" },
            new object?[] { 2, "A2" },
            new object?[] { 3, "A3" }
        };
        var columnTypes = new ObservableCollection<Type> { typeof(int), typeof(string) };

        var text = builder.Build(
            [new GridSelectionRange(new GridCellAddress(0, 0), new GridCellAddress(2, 0))],
            rows,
            columnTypes);

        Assert.Equal("1\n2\n3", text);
    }
}
