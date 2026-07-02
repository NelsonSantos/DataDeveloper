using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Models.TableDesigner;
using Xunit;

namespace DataDeveloper.Tests;

public class ProviderDataTypeCatalogTests
{
    [Theory]
    [InlineData(DatabaseType.SqlServer)]
    [InlineData(DatabaseType.MySql)]
    [InlineData(DatabaseType.PostgresSql)]
    [InlineData(DatabaseType.Oracle)]
    [InlineData(DatabaseType.SqLite)]
    public void GetDataTypes_ReturnsOptionsWithoutDuplicateNames(DatabaseType databaseType)
    {
        var types = ProviderDataTypeCatalog.GetDataTypes(databaseType);

        Assert.NotEmpty(types);
        Assert.Equal(
            types.Count,
            types.Select(type => type.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Theory]
    [InlineData(DatabaseType.SqlServer)]
    [InlineData(DatabaseType.MySql)]
    [InlineData(DatabaseType.PostgresSql)]
    [InlineData(DatabaseType.Oracle)]
    [InlineData(DatabaseType.SqLite)]
    public void GetDefaultDataType_ReturnsTypeFromProviderCatalog(DatabaseType databaseType)
    {
        var types = ProviderDataTypeCatalog.GetDataTypes(databaseType);
        var defaultType = ProviderDataTypeCatalog.GetDefaultDataType(databaseType);

        Assert.Contains(types, type => string.Equals(type.Name, defaultType.Name, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(DatabaseType.SqlServer, "int")]
    [InlineData(DatabaseType.MySql, "int")]
    [InlineData(DatabaseType.PostgresSql, "integer")]
    [InlineData(DatabaseType.Oracle, "number")]
    [InlineData(DatabaseType.SqLite, "integer")]
    public void GetDefaultDataType_ReturnsExpectedProviderDefault(DatabaseType databaseType, string expectedName)
    {
        var defaultType = ProviderDataTypeCatalog.GetDefaultDataType(databaseType);

        Assert.Equal(expectedName, defaultType.Name);
    }

    [Theory]
    [InlineData(DatabaseType.SqlServer)]
    [InlineData(DatabaseType.MySql)]
    [InlineData(DatabaseType.PostgresSql)]
    [InlineData(DatabaseType.Oracle)]
    [InlineData(DatabaseType.SqLite)]
    public void GetDataTypes_ReturnsOptionsOrderedByName(DatabaseType databaseType)
    {
        var names = ProviderDataTypeCatalog.GetDataTypes(databaseType)
            .Select(type => type.Name)
            .ToList();

        Assert.Equal(names.OrderBy(name => name, StringComparer.OrdinalIgnoreCase), names);
    }

    [Theory]
    [InlineData(DatabaseType.SqlServer, "int")]
    [InlineData(DatabaseType.MySql, "int")]
    [InlineData(DatabaseType.PostgresSql, "integer")]
    [InlineData(DatabaseType.Oracle, "number")]
    [InlineData(DatabaseType.SqLite, "integer")]
    public void GetDataTypes_IncludesIdentityCapableType(DatabaseType databaseType, string typeName)
    {
        var type = ProviderDataTypeCatalog.GetDataTypes(databaseType)
            .Single(item => string.Equals(item.Name, typeName, StringComparison.OrdinalIgnoreCase));

        Assert.True(type.SupportsIdentity);
    }

    [Theory]
    [InlineData(DatabaseType.SqlServer, "varchar")]
    [InlineData(DatabaseType.MySql, "varchar")]
    [InlineData(DatabaseType.PostgresSql, "varchar")]
    [InlineData(DatabaseType.Oracle, "varchar2")]
    public void GetDataTypes_IncludesLengthCapableStringType(DatabaseType databaseType, string typeName)
    {
        var type = ProviderDataTypeCatalog.GetDataTypes(databaseType)
            .Single(item => string.Equals(item.Name, typeName, StringComparison.OrdinalIgnoreCase));

        Assert.True(type.SupportsLength);
        Assert.NotNull(type.DefaultLength);
    }

    [Theory]
    [InlineData(DatabaseType.SqlServer, "decimal")]
    [InlineData(DatabaseType.MySql, "decimal")]
    [InlineData(DatabaseType.PostgresSql, "numeric")]
    [InlineData(DatabaseType.Oracle, "number")]
    public void GetDataTypes_IncludesPrecisionCapableNumericType(DatabaseType databaseType, string typeName)
    {
        var type = ProviderDataTypeCatalog.GetDataTypes(databaseType)
            .Single(item => string.Equals(item.Name, typeName, StringComparison.OrdinalIgnoreCase));

        Assert.True(type.SupportsPrecision);
        Assert.NotNull(type.DefaultPrecision);
    }

    [Theory]
    [InlineData(DatabaseType.SqlServer, "datetimeoffset")]
    [InlineData(DatabaseType.MySql, "mediumint")]
    [InlineData(DatabaseType.PostgresSql, "json")]
    [InlineData(DatabaseType.Oracle, "timestamp with local time zone")]
    [InlineData(DatabaseType.SqLite, "numeric")]
    public void GetDataTypes_IncludesProviderSpecificCommonTypes(DatabaseType databaseType, string typeName)
    {
        Assert.Contains(
            ProviderDataTypeCatalog.GetDataTypes(databaseType),
            type => string.Equals(type.Name, typeName, StringComparison.OrdinalIgnoreCase));
    }
}
