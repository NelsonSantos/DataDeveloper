using DataDeveloper.Data.Services.FileImport;
using Xunit;

namespace DataDeveloper.Tests.FileImport;

public class FileImportTableNameSanitizerTests
{
    [Theory]
    [InlineData("distribusion_cities.backup-20260727-160029", "distribusion_cities_backup_20260727_160029")]
    [InlineData("Orders 2026", "Orders_2026")]
    [InlineData("customers", "customers")]
    [InlineData("my..file", "my_file")]
    [InlineData("__leading_and_trailing__", "leading_and_trailing")]
    [InlineData("2026-report", "_2026_report")]
    [InlineData("!!!", "imported_table")]
    [InlineData("", "imported_table")]
    public void Sanitize_ProducesASafeSingleIdentifier(string fileName, string expected)
    {
        Assert.Equal(expected, FileImportTableNameSanitizer.Sanitize(fileName));
    }

    [Fact]
    public void Sanitize_NeverContainsADot()
    {
        var result = FileImportTableNameSanitizer.Sanitize("cities.backup-2026.old");

        Assert.DoesNotContain('.', result);
    }
}
