using DataDeveloper.Data.Services.FileImport;
using DataDeveloper.Services.GridExport;
using Xunit;

namespace DataDeveloper.Tests.GridExport;

public class GridExportWriterTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    public void Dispose()
    {
        foreach (var path in _tempFiles.Where(File.Exists))
            File.Delete(path);
    }

    [Fact]
    public async Task WriteCsvAsync_PlainValues_NoQuotingNeeded()
    {
        var path = CreateTempPath(".csv");

        await GridExportWriter.WriteCsvAsync(
            path,
            new[] { "Id", "Name" },
            new List<IReadOnlyList<object?>> { new object?[] { 1, "Alice" } },
            new Type?[] { typeof(int), typeof(string) });

        var content = await File.ReadAllTextAsync(path);
        Assert.Equal("Id,Name\r\n1,Alice\r\n", content);
    }

    [Fact]
    public async Task WriteCsvAsync_ValueWithComma_IsQuoted()
    {
        var path = CreateTempPath(".csv");

        await GridExportWriter.WriteCsvAsync(
            path,
            new[] { "Name" },
            new List<IReadOnlyList<object?>> { new object?[] { "Doe, John" } },
            new Type?[] { typeof(string) });

        var content = await File.ReadAllTextAsync(path);
        Assert.Equal("Name\r\n\"Doe, John\"\r\n", content);
    }

    [Fact]
    public async Task WriteCsvAsync_ValueWithQuotes_IsQuotedAndEscaped()
    {
        var path = CreateTempPath(".csv");

        await GridExportWriter.WriteCsvAsync(
            path,
            new[] { "Name" },
            new List<IReadOnlyList<object?>> { new object?[] { "6\" pipe" } },
            new Type?[] { typeof(string) });

        var content = await File.ReadAllTextAsync(path);
        Assert.Equal("Name\r\n\"6\"\" pipe\"\r\n", content);
    }

    [Fact]
    public async Task WriteCsvAsync_ValueWithNewline_IsQuoted()
    {
        var path = CreateTempPath(".csv");

        await GridExportWriter.WriteCsvAsync(
            path,
            new[] { "Notes" },
            new List<IReadOnlyList<object?>> { new object?[] { "line1\nline2" } },
            new Type?[] { typeof(string) });

        var content = await File.ReadAllTextAsync(path);
        Assert.Equal("Notes\r\n\"line1\nline2\"\r\n", content);
    }

    [Fact]
    public async Task WriteCsvAsync_NullValue_WritesEmptyUnquotedField()
    {
        var path = CreateTempPath(".csv");

        await GridExportWriter.WriteCsvAsync(
            path,
            new[] { "Id", "Notes" },
            new List<IReadOnlyList<object?>> { new object?[] { 1, null } },
            new Type?[] { typeof(int), typeof(string) });

        var content = await File.ReadAllTextAsync(path);
        Assert.Equal("Id,Notes\r\n1,\r\n", content);
    }

    [Fact]
    public async Task WriteXlsxAsync_RoundTripsHeadersAndNativeNumericValues()
    {
        var path = CreateTempPath(".xlsx");

        await GridExportWriter.WriteXlsxAsync(
            path,
            new[] { "Id", "Name", "IsActive" },
            new List<IReadOnlyList<object?>>
            {
                new object?[] { 1, "Alice", true },
                new object?[] { 2, "Bob", false }
            },
            new Type?[] { typeof(int), typeof(string), typeof(bool) });

        var preview = FileImportReader.ReadPreview(path, sampleRowCount: 10);

        Assert.Equal(new[] { "Id", "Name", "IsActive" }, preview.Headers);
        Assert.Equal(2, preview.SampleRows.Count);
        // Native typed numeric cell round-trips as a double, not the display string "1".
        Assert.Equal(1d, preview.SampleRows[0][0]);
        Assert.Equal("Alice", preview.SampleRows[0][1]);
        Assert.Equal(2d, preview.SampleRows[1][0]);
        Assert.Equal("Bob", preview.SampleRows[1][1]);
    }

    [Fact]
    public async Task WriteXlsxAsync_ByteArrayValue_WritesHexTextInsteadOfLosingData()
    {
        var path = CreateTempPath(".xlsx");

        await GridExportWriter.WriteXlsxAsync(
            path,
            new[] { "Data" },
            new List<IReadOnlyList<object?>> { new object?[] { new byte[] { 0x0A, 0xFF } } },
            new Type?[] { typeof(byte[]) });

        var preview = FileImportReader.ReadPreview(path, sampleRowCount: 10);

        Assert.Equal("0x0AFF", preview.SampleRows[0][0]);
    }

    private string CreateTempPath(string extension)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{extension}");
        _tempFiles.Add(path);
        return path;
    }
}
