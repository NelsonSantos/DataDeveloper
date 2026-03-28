using System.Globalization;
using DataDeveloper.NextGrid.Renderers;
using Xunit;

namespace DataDeveloper.Tests.NextGrid.Renderers;

public sealed class GridRenderersFormattingTests
{
    [Fact]
    public void NumberRenderer_FormatsUsingContextCulture()
    {
        var renderer = new NumberGridCellRenderer();
        var context = new GridRendererContext(new CultureInfo("pt-BR"));

        var text = renderer.FormatValue(227.5m, context);

        Assert.Equal("227,5", text);
    }

    [Fact]
    public void DateTimeRenderer_FormatsUsingContextCulture()
    {
        var renderer = new DateTimeGridCellRenderer();
        var context = new GridRendererContext(new CultureInfo("pt-BR"));

        var text = renderer.FormatValue(new DateTime(2025, 8, 14, 0, 55, 3), context);

        Assert.Contains("14/08/2025", text);
    }

    [Fact]
    public void MeasureWidth_UsesFormattedValue()
    {
        var renderer = new NumberGridCellRenderer();
        var context = GridRendererContext.Default;

        var width = renderer.MeasureWidth(1234, context, text => text.Length * 10);

        Assert.Equal(40, width);
    }
}
