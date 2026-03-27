using DataDeveloper.NextGrid.Editors;
using Xunit;

namespace DataDeveloper.Tests.NextGrid.Editors;

public sealed class GridEditorRegistryTests
{
    [Fact]
    public void Resolve_ReturnsTextEditorForString()
    {
        var registry = new GridEditorRegistry();

        var editor = registry.Resolve(typeof(string), "abc");

        Assert.IsType<TextGridCellEditor>(editor);
    }

    [Fact]
    public void Resolve_ReturnsNumberEditorForDecimal()
    {
        var registry = new GridEditorRegistry();

        var editor = registry.Resolve(typeof(decimal), 10.5m);

        Assert.IsType<NumberGridCellEditor>(editor);
    }

    [Fact]
    public void Resolve_ReturnsDateTimeEditorForDateTime()
    {
        var registry = new GridEditorRegistry();

        var editor = registry.Resolve(typeof(DateTime), DateTime.Now);

        Assert.IsType<DateTimeGridCellEditor>(editor);
    }
}
