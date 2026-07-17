using DataDeveloper.Data.Services.SchemaCompare;
using Xunit;

namespace DataDeveloper.Tests.SchemaCompare;

public class SchemaCompareObjectNameMatcherTests
{
    [Theory]
    [InlineData("[dbo].[Orders]", "dbo].[orders")]
    [InlineData("Orders", "orders")]
    [InlineData("  Orders  ", "orders")]
    public void Normalize_TrimsWrapperCharactersWhitespaceAndLowercases(string input, string expected)
    {
        Assert.Equal(expected, SchemaCompareObjectNameMatcher.Normalize(input));
    }

    [Theory]
    [InlineData("[dbo].[Orders]", "dbo].[Orders")]
    [InlineData("`orders`", "ORDERS")]
    [InlineData("\"Orders\"", "orders")]
    [InlineData("Orders", "  orders  ")]
    public void AreEqual_IsCaseInsensitiveAndIgnoresWrapperCharacters(string a, string b)
    {
        Assert.True(SchemaCompareObjectNameMatcher.AreEqual(a, b));
    }

    [Fact]
    public void AreEqual_ReturnsFalseForDifferentNames()
    {
        Assert.False(SchemaCompareObjectNameMatcher.AreEqual("Orders", "Customers"));
    }
}
