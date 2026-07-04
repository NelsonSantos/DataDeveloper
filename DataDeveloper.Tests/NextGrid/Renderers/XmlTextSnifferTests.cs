using DataDeveloper.NextGrid.Renderers;
using Xunit;

namespace DataDeveloper.Tests.NextGrid.Renderers;

public sealed class XmlTextSnifferTests
{
    [Theory]
    [InlineData("<root/>")]
    [InlineData("<root><a>1</a></root>")]
    [InlineData("  <root>\n  <a>1</a>\n</root>  ")]
    [InlineData("<?xml version=\"1.0\"?><root><a>1</a></root>")]
    public void IsLikelyXml_ReturnsTrueForValidXml(string text)
    {
        Assert.True(XmlTextSniffer.IsLikelyXml(text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("plain text")]
    [InlineData("{\"a\":1}")]
    [InlineData("<root>")]
    [InlineData("<root><a>1</a>")]
    public void IsLikelyXml_ReturnsFalseForNonXmlOrMalformedText(string? text)
    {
        Assert.False(XmlTextSniffer.IsLikelyXml(text));
    }
}
