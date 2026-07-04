using DataDeveloper.NextGrid.Renderers;
using Xunit;

namespace DataDeveloper.Tests.NextGrid.Renderers;

public sealed class StructuredTextSnifferTests
{
    [Theory]
    [InlineData("{\"a\":1}")]
    [InlineData("[1,2,3]")]
    public void Detect_ReturnsJsonForValidJson(string text)
    {
        Assert.Equal(StructuredTextKind.Json, StructuredTextSniffer.Detect(text));
    }

    [Theory]
    [InlineData("<root><a>1</a></root>")]
    [InlineData("<root/>")]
    public void Detect_ReturnsXmlForValidXml(string text)
    {
        Assert.Equal(StructuredTextKind.Xml, StructuredTextSniffer.Detect(text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("plain text")]
    [InlineData("{\"a\":1,")]
    [InlineData("<root>")]
    public void Detect_ReturnsNoneForPlainOrMalformedText(string? text)
    {
        Assert.Equal(StructuredTextKind.None, StructuredTextSniffer.Detect(text));
    }
}
