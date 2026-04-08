using DataDeveloper.NextGrid.Renderers;
using Xunit;

namespace DataDeveloper.Tests.NextGrid.Renderers;

public sealed class GridRendererRegistryTests
{
    [Fact]
    public void Resolve_ReturnsTextRendererForString()
    {
        var registry = new GridRendererRegistry();

        var renderer = registry.Resolve(typeof(string), "abc");

        Assert.IsType<TextGridCellRenderer>(renderer);
        Assert.Equal(GridColumnAlignment.Left, renderer.Alignment);
    }

    [Fact]
    public void Resolve_ReturnsNumberRendererForDecimal()
    {
        var registry = new GridRendererRegistry();

        var renderer = registry.Resolve(typeof(decimal), 123.45m);

        Assert.IsType<NumberGridCellRenderer>(renderer);
        Assert.Equal(GridColumnAlignment.Right, renderer.Alignment);
    }

    [Fact]
    public void Resolve_ReturnsBooleanRendererForBool()
    {
        var registry = new GridRendererRegistry();

        var renderer = registry.Resolve(typeof(bool), true);

        Assert.IsType<BooleanGridCellRenderer>(renderer);
        Assert.Equal(GridColumnAlignment.Center, renderer.Alignment);
    }

    [Fact]
    public void Resolve_ReturnsNullRendererForDBNull()
    {
        var registry = new GridRendererRegistry();

        var renderer = registry.Resolve(typeof(DBNull), DBNull.Value);

        Assert.IsType<NullGridCellRenderer>(renderer);
    }

    [Fact]
    public void Resolve_ReturnsDateTimeRendererForDateTime()
    {
        var registry = new GridRendererRegistry();

        var renderer = registry.Resolve(typeof(DateTime), new DateTime(2025, 8, 14, 0, 55, 3));

        Assert.IsType<DateTimeGridCellRenderer>(renderer);
    }

    [Fact]
    public void Resolve_ReturnsNumberRendererForNullableDecimalWithNullValue()
    {
        var registry = new GridRendererRegistry();

        var renderer = registry.Resolve(typeof(decimal?), null);

        Assert.IsType<NumberGridCellRenderer>(renderer);
        Assert.Equal(GridColumnAlignment.Right, renderer.Alignment);
    }
}
