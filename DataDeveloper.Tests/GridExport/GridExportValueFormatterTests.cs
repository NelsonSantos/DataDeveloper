using DataDeveloper.NextGrid.Renderers;
using DataDeveloper.Services.GridExport;
using Xunit;

namespace DataDeveloper.Tests.GridExport;

public class GridExportValueFormatterTests
{
    private readonly GridExportValueFormatter _formatter = new(new GridRendererRegistry());

    [Fact]
    public void Format_ByteArray_ReturnsHexPrefixed()
    {
        var result = _formatter.Format(new byte[] { 0x0A, 0xFF }, typeof(byte[]));

        Assert.Equal("0x0AFF", result);
    }

    [Fact]
    public void Format_EmptyByteArray_ReturnsHexPrefix()
    {
        var result = _formatter.Format(Array.Empty<byte>(), typeof(byte[]));

        Assert.Equal("0x", result);
    }

    [Theory]
    [InlineData(typeof(string))]
    [InlineData(typeof(int))]
    [InlineData(null)]
    public void Format_Null_ReturnsEmptyString(Type? valueType)
    {
        var result = _formatter.Format(null, valueType);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Format_Number_DelegatesToRendererRegistry()
    {
        var result = _formatter.Format(42, typeof(int));

        Assert.Equal("42", result);
    }

    [Fact]
    public void Format_DateTime_DelegatesToRendererRegistry()
    {
        var result = _formatter.Format(new DateTime(2026, 1, 2, 3, 4, 5), typeof(DateTime));

        Assert.Equal("2026-01-02 03:04:05", result);
    }

    [Fact]
    public void Format_Boolean_DelegatesToRendererRegistry()
    {
        var result = _formatter.Format(true, typeof(bool));

        Assert.Equal("True", result);
    }
}
