using DataDeveloper.Services;
using Xunit;

namespace DataDeveloper.Tests;

public class SqlFunctionCallContextDetectorTests
{
    [Fact]
    public void Detect_AfterOpenParen_ReturnsFunctionAndFirstArgument()
    {
        var sql = "select sum(";

        var context = SqlFunctionCallContextDetector.Detect(sql, sql.Length);

        Assert.NotNull(context);
        Assert.Equal("sum", context!.FunctionName);
        Assert.Equal(0, context.ArgumentIndex);
    }

    [Fact]
    public void Detect_AfterComma_ReturnsSecondArgument()
    {
        var sql = "select dateadd(day, ";

        var context = SqlFunctionCallContextDetector.Detect(sql, sql.Length);

        Assert.NotNull(context);
        Assert.Equal("dateadd", context!.FunctionName);
        Assert.Equal(1, context.ArgumentIndex);
    }

    [Fact]
    public void Detect_IgnoresNestedComma_ReturnsOuterFunctionArgument()
    {
        var sql = "select coalesce(nullif(name, ''), ";

        var context = SqlFunctionCallContextDetector.Detect(sql, sql.Length);

        Assert.NotNull(context);
        Assert.Equal("coalesce", context!.FunctionName);
        Assert.Equal(1, context.ArgumentIndex);
    }

    [Fact]
    public void Detect_IgnoresCommaInsideString()
    {
        var sql = "select coalesce('a,b', ";

        var context = SqlFunctionCallContextDetector.Detect(sql, sql.Length);

        Assert.NotNull(context);
        Assert.Equal("coalesce", context!.FunctionName);
        Assert.Equal(1, context.ArgumentIndex);
    }

    [Fact]
    public void Detect_IgnoresFunctionLikeTextInsideComment()
    {
        var sql = "select -- coalesce(\ncount(";

        var context = SqlFunctionCallContextDetector.Detect(sql, sql.Length);

        Assert.NotNull(context);
        Assert.Equal("count", context!.FunctionName);
        Assert.Equal(0, context.ArgumentIndex);
    }

    [Fact]
    public void Detect_AfterClosedParen_ReturnsNull()
    {
        var sql = "select sum(value)";

        var context = SqlFunctionCallContextDetector.Detect(sql, sql.Length);

        Assert.Null(context);
    }

    [Fact]
    public void Detect_ReturnsInnermostFunction()
    {
        var sql = "select coalesce(nullif(";

        var context = SqlFunctionCallContextDetector.Detect(sql, sql.Length);

        Assert.NotNull(context);
        Assert.Equal("nullif", context!.FunctionName);
        Assert.Equal(0, context.ArgumentIndex);
    }
}
