using DataDeveloper.Helpers;
using Xunit;

namespace DataDeveloper.Tests;

public sealed class XmlTextFormatterTests
{
    [Fact]
    public void TryFormat_Indented_PrettyPrintsValidXml()
    {
        var success = XmlTextFormatter.TryFormat("<root><a>1</a><b>2</b></root>", indented: true, out var result);

        Assert.True(success);
        Assert.Contains("\n", result);
        Assert.Contains("<a>1</a>", result);
    }

    [Fact]
    public void TryFormat_NotIndented_MinifiesValidXml()
    {
        var success = XmlTextFormatter.TryFormat("<root>\n  <a>1</a>\n  <b>2</b>\n</root>", indented: false, out var result);

        Assert.True(success);
        Assert.DoesNotContain("\n", result);
    }

    [Fact]
    public void TryFormat_ReturnsFalseAndOriginalTextForInvalidXml()
    {
        var success = XmlTextFormatter.TryFormat("not xml", indented: true, out var result);

        Assert.False(success);
        Assert.Equal("not xml", result);
    }

    [Fact]
    public void TryFormat_ReturnsFalseForEmptyText()
    {
        var success = XmlTextFormatter.TryFormat(string.Empty, indented: true, out var result);

        Assert.False(success);
        Assert.Equal(string.Empty, result);
    }

    [Theory]
    [InlineData("<root><a>1</a></root>", true)]
    [InlineData("<root/>", true)]
    [InlineData("not xml", false)]
    [InlineData("<root>", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidXml_ReflectsWhetherTextParses(string? text, bool expected)
    {
        Assert.Equal(expected, XmlTextFormatter.IsValidXml(text));
    }
}
