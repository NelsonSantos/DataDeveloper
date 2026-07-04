using DataDeveloper.Helpers;
using Xunit;

namespace DataDeveloper.Tests;

public sealed class JsonTextFormatterTests
{
    [Fact]
    public void TryFormat_Indented_PrettyPrintsValidJson()
    {
        var success = JsonTextFormatter.TryFormat("{\"a\":1,\"b\":[1,2]}", indented: true, out var result);

        Assert.True(success);
        Assert.Contains("\n", result);
        Assert.Contains("\"a\"", result);
    }

    [Fact]
    public void TryFormat_NotIndented_MinifiesValidJson()
    {
        var success = JsonTextFormatter.TryFormat("{\n  \"a\": 1,\n  \"b\": [1, 2]\n}", indented: false, out var result);

        Assert.True(success);
        Assert.DoesNotContain("\n", result);
        Assert.Equal("{\"a\":1,\"b\":[1,2]}", result);
    }

    [Fact]
    public void TryFormat_ReturnsFalseAndOriginalTextForInvalidJson()
    {
        var success = JsonTextFormatter.TryFormat("not json", indented: true, out var result);

        Assert.False(success);
        Assert.Equal("not json", result);
    }

    [Fact]
    public void TryFormat_ReturnsFalseForEmptyText()
    {
        var success = JsonTextFormatter.TryFormat(string.Empty, indented: true, out var result);

        Assert.False(success);
        Assert.Equal(string.Empty, result);
    }

    [Theory]
    [InlineData("{\"a\":1}", true)]
    [InlineData("[1,2,3]", true)]
    [InlineData("not json", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidJson_ReflectsWhetherTextParses(string? text, bool expected)
    {
        Assert.Equal(expected, JsonTextFormatter.IsValidJson(text));
    }
}
