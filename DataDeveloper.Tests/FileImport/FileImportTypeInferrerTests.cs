using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Models.TableDesigner;
using DataDeveloper.Data.Services.FileImport;
using Xunit;

namespace DataDeveloper.Tests.FileImport;

public class FileImportTypeInferrerTests
{
    [Fact]
    public void InferType_AllIntegerValues_ReturnsInteger()
    {
        var result = FileImportTypeInferrer.InferType(new object?[] { "1", "2", 3d, null });

        Assert.Equal(FileImportInferredType.Integer, result);
    }

    [Fact]
    public void InferType_MixedIntegerAndDecimalValues_ReturnsDecimal()
    {
        var result = FileImportTypeInferrer.InferType(new object?[] { "1", "2.5", 3.1d });

        Assert.Equal(FileImportInferredType.Decimal, result);
    }

    [Fact]
    public void InferType_DateStrings_ReturnsDateTime()
    {
        var result = FileImportTypeInferrer.InferType(new object?[] { "2024-01-01", "2024-02-15", new DateTime(2024, 3, 1) });

        Assert.Equal(FileImportInferredType.DateTime, result);
    }

    [Fact]
    public void InferType_BooleanValues_ReturnsBoolean()
    {
        var result = FileImportTypeInferrer.InferType(new object?[] { "true", "false", true });

        Assert.Equal(FileImportInferredType.Boolean, result);
    }

    [Fact]
    public void InferType_MixedIncompatibleValues_ReturnsText()
    {
        var result = FileImportTypeInferrer.InferType(new object?[] { "1", "not-a-number", "2024-01-01" });

        Assert.Equal(FileImportInferredType.Text, result);
    }

    [Fact]
    public void InferType_AllNullOrBlank_ReturnsText()
    {
        var result = FileImportTypeInferrer.InferType(new object?[] { null, "", "   " });

        Assert.Equal(FileImportInferredType.Text, result);
    }

    [Theory]
    [InlineData(DatabaseType.SqlServer, FileImportInferredType.Integer, "bigint")]
    [InlineData(DatabaseType.SqlServer, FileImportInferredType.Decimal, "decimal")]
    [InlineData(DatabaseType.SqlServer, FileImportInferredType.DateTime, "datetime2")]
    [InlineData(DatabaseType.SqlServer, FileImportInferredType.Boolean, "bit")]
    [InlineData(DatabaseType.SqlServer, FileImportInferredType.Text, "nvarchar")]
    [InlineData(DatabaseType.MySql, FileImportInferredType.Integer, "bigint")]
    [InlineData(DatabaseType.MySql, FileImportInferredType.Decimal, "decimal")]
    [InlineData(DatabaseType.MySql, FileImportInferredType.DateTime, "datetime")]
    [InlineData(DatabaseType.MySql, FileImportInferredType.Boolean, "boolean")]
    [InlineData(DatabaseType.MySql, FileImportInferredType.Text, "varchar")]
    [InlineData(DatabaseType.PostgresSql, FileImportInferredType.Integer, "bigint")]
    [InlineData(DatabaseType.PostgresSql, FileImportInferredType.Decimal, "numeric")]
    [InlineData(DatabaseType.PostgresSql, FileImportInferredType.DateTime, "timestamp")]
    [InlineData(DatabaseType.PostgresSql, FileImportInferredType.Boolean, "boolean")]
    [InlineData(DatabaseType.PostgresSql, FileImportInferredType.Text, "varchar")]
    [InlineData(DatabaseType.Oracle, FileImportInferredType.Integer, "number")]
    [InlineData(DatabaseType.Oracle, FileImportInferredType.Decimal, "number")]
    [InlineData(DatabaseType.Oracle, FileImportInferredType.DateTime, "timestamp")]
    [InlineData(DatabaseType.Oracle, FileImportInferredType.Boolean, "number")]
    [InlineData(DatabaseType.Oracle, FileImportInferredType.Text, "varchar2")]
    [InlineData(DatabaseType.SqLite, FileImportInferredType.Integer, "integer")]
    [InlineData(DatabaseType.SqLite, FileImportInferredType.Decimal, "numeric")]
    [InlineData(DatabaseType.SqLite, FileImportInferredType.DateTime, "text")]
    [InlineData(DatabaseType.SqLite, FileImportInferredType.Boolean, "integer")]
    [InlineData(DatabaseType.SqLite, FileImportInferredType.Text, "text")]
    public void GetSuggestedDataTypeName_ReturnsExpectedTypePerProvider(
        DatabaseType databaseType, FileImportInferredType inferredType, string expectedTypeName)
    {
        Assert.Equal(expectedTypeName, FileImportTypeInferrer.GetSuggestedDataTypeName(databaseType, inferredType));
    }

    public static IEnumerable<object[]> AllProvidersAndInferredTypes()
    {
        foreach (var databaseType in Enum.GetValues<DatabaseType>())
        foreach (var inferredType in Enum.GetValues<FileImportInferredType>())
            yield return new object[] { databaseType, inferredType };
    }

    [Theory]
    [MemberData(nameof(AllProvidersAndInferredTypes))]
    public void GetSuggestedDataTypeName_AlwaysResolvesToAKnownProviderType(DatabaseType databaseType, FileImportInferredType inferredType)
    {
        var typeName = FileImportTypeInferrer.GetSuggestedDataTypeName(databaseType, inferredType);
        var knownTypes = ProviderDataTypeCatalog.GetDataTypes(databaseType);

        Assert.Contains(knownTypes, option => string.Equals(option.Name, typeName, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SuggestColumn_ForSqlServerDecimal_PopulatesDefaultPrecisionAndScale()
    {
        var column = FileImportTypeInferrer.SuggestColumn(DatabaseType.SqlServer, "Amount", new object?[] { "1.5", "2.75" });

        Assert.Equal("Amount", column.Name);
        Assert.Equal("decimal", column.DataType);
        Assert.Equal(18, column.Precision);
        Assert.Equal(2, column.Scale);
        Assert.True(column.IsNullable);
    }

    [Fact]
    public void SuggestColumn_ForSqLiteText_LeavesLengthPrecisionScaleNull()
    {
        var column = FileImportTypeInferrer.SuggestColumn(DatabaseType.SqLite, "Notes", new object?[] { "hello", "world" });

        Assert.Equal("text", column.DataType);
        Assert.Null(column.Length);
        Assert.Null(column.Precision);
        Assert.Null(column.Scale);
    }
}
