using System.Linq;
using DataDeveloper.Services;
using Xunit;

namespace DataDeveloper.Tests;

public sealed class JsonFoldingStrategyTests
{
    [Fact]
    public void CreateNewFoldings_CreatesFoldingForMultiLineObject()
    {
        var text = "{\n  \"a\": 1\n}";

        var foldings = JsonFoldingStrategy.CreateNewFoldings(text).ToList();

        var folding = Assert.Single(foldings);
        Assert.Equal(0, folding.StartOffset);
        Assert.Equal(text.Length, folding.EndOffset);
    }

    [Fact]
    public void CreateNewFoldings_SkipsSingleLineObject()
    {
        var text = "{\"a\":1,\"b\":2}";

        var foldings = JsonFoldingStrategy.CreateNewFoldings(text).ToList();

        Assert.Empty(foldings);
    }

    [Fact]
    public void CreateNewFoldings_CreatesNestedFoldingsForMultiLineArrayAndObject()
    {
        var text = "{\n  \"items\": [\n    1,\n    2\n  ]\n}";

        var foldings = JsonFoldingStrategy.CreateNewFoldings(text)
            .OrderBy(f => f.StartOffset)
            .ToList();

        Assert.Equal(2, foldings.Count);
        Assert.Equal(0, foldings[0].StartOffset);
        Assert.Equal(text.Length, foldings[0].EndOffset);

        var arrayStart = text.IndexOf('[');
        var arrayEnd = text.IndexOf(']');
        Assert.Equal(arrayStart, foldings[1].StartOffset);
        Assert.Equal(arrayEnd + 1, foldings[1].EndOffset);
    }

    [Fact]
    public void CreateNewFoldings_IgnoresBracesInsideStringValues()
    {
        var text = "{\n  \"a\": \"{ not a fold }\"\n}";

        var foldings = JsonFoldingStrategy.CreateNewFoldings(text).ToList();

        var folding = Assert.Single(foldings);
        Assert.Equal(0, folding.StartOffset);
        Assert.Equal(text.Length, folding.EndOffset);
    }
}
