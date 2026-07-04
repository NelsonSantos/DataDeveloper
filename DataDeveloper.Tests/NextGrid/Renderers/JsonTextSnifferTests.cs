using DataDeveloper.NextGrid.Renderers;
using Xunit;

namespace DataDeveloper.Tests.NextGrid.Renderers;

public sealed class JsonTextSnifferTests
{
    [Theory]
    [InlineData("{\"id\":1}")]
    [InlineData("[1,2,3]")]
    [InlineData("  { \"a\" : [1, 2, { \"b\": null }] }  ")]
    [InlineData("[]")]
    [InlineData("{}")]
    public void IsLikelyJson_ReturnsTrueForValidJsonObjectsAndArrays(string text)
    {
        Assert.True(JsonTextSniffer.IsLikelyJson(text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("plain text")]
    [InlineData("123")]
    [InlineData("\"just a string\"")]
    [InlineData("{\"id\":1,")]
    [InlineData("{not valid json}")]
    public void IsLikelyJson_ReturnsFalseForNonJsonOrMalformedText(string? text)
    {
        Assert.False(JsonTextSniffer.IsLikelyJson(text));
    }
}
