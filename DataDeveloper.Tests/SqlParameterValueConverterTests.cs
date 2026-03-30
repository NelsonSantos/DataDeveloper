using System;
using DataDeveloper.Services;
using Xunit;

namespace DataDeveloper.Tests;

public class SqlParameterValueConverterTests
{
    [Fact]
    public void Convert_ReturnsNull_WhenNullFlagIsTrue()
    {
        var result = SqlParameterValueConverter.Convert("2026-01-31 23:59:59", true);

        Assert.Null(result);
    }

    [Fact]
    public void Convert_ParsesDateTime_FromUnquotedText()
    {
        var result = SqlParameterValueConverter.Convert("2026-01-31 23:59:59", false);

        var dateTime = Assert.IsType<DateTime>(result);
        Assert.Equal(new DateTime(2026, 1, 31, 23, 59, 59), dateTime);
    }

    [Fact]
    public void Convert_ParsesDateTime_FromSingleQuotedText()
    {
        var result = SqlParameterValueConverter.Convert("'2026-01-31 23:59:59'", false);

        var dateTime = Assert.IsType<DateTime>(result);
        Assert.Equal(new DateTime(2026, 1, 31, 23, 59, 59), dateTime);
    }
}
