using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Providers.MySql;
using DataDeveloper.Data.Providers.Oracle;
using DataDeveloper.Data.Providers.PostgresSql;
using DataDeveloper.Data.Providers.SqLite;
using DataDeveloper.Data.Providers.SqlServer;
using DataDeveloper.Data.Services.SqlDialects;
using Xunit;

namespace DataDeveloper.Tests;

public class SqlDialectTests
{
    [Theory]
    [InlineData(DatabaseType.SqlServer, typeof(SqlServerSqlDialect))]
    [InlineData(DatabaseType.Oracle, typeof(OracleSqlDialect))]
    [InlineData(DatabaseType.PostgresSql, typeof(PostgresSqlDialect))]
    [InlineData(DatabaseType.MySql, typeof(MySqlSqlDialect))]
    [InlineData(DatabaseType.SqLite, typeof(SqLiteSqlDialect))]
    public void For_ReturnsProviderDialect(DatabaseType databaseType, Type expectedType)
    {
        Assert.IsType(expectedType, SqlDialect.For(databaseType));
    }

    [Fact]
    public void For_ReturnsSameInstanceForSameProvider()
    {
        Assert.Same(SqlDialect.For(DatabaseType.PostgresSql), SqlDialect.For(DatabaseType.PostgresSql));
    }

    [Theory]
    [InlineData(DatabaseType.SqlServer, "Order Items", "[Order Items]")]
    [InlineData(DatabaseType.SqlServer, "a]b", "[a]]b]")]
    [InlineData(DatabaseType.Oracle, "MixedCase", "\"MixedCase\"")]
    [InlineData(DatabaseType.Oracle, "a\"b", "\"a\"\"b\"")]
    [InlineData(DatabaseType.PostgresSql, "MixedCase", "\"MixedCase\"")]
    [InlineData(DatabaseType.PostgresSql, "a\"b", "\"a\"\"b\"")]
    [InlineData(DatabaseType.MySql, "order items", "`order items`")]
    [InlineData(DatabaseType.MySql, "a`b", "`a``b`")]
    [InlineData(DatabaseType.SqLite, "MixedCase", "\"MixedCase\"")]
    [InlineData(DatabaseType.SqLite, "a\"b", "\"a\"\"b\"")]
    public void QuoteIdentifier_DelimitsAndEscapesKeepingCase(DatabaseType databaseType, string identifier, string expected)
    {
        Assert.Equal(expected, SqlDialect.For(databaseType).QuoteIdentifier(identifier));
    }

    [Theory]
    [InlineData(DatabaseType.SqlServer, "Customers", "[Customers]")]
    [InlineData(DatabaseType.PostgresSql, "customers", "\"customers\"")]
    [InlineData(DatabaseType.MySql, "customers", "`customers`")]
    [InlineData(DatabaseType.SqLite, "customers", "\"customers\"")]
    public void FormatIdentifier_MatchesQuoteIdentifierOutsideOracle(DatabaseType databaseType, string identifier, string expected)
    {
        Assert.Equal(expected, SqlDialect.For(databaseType).FormatIdentifier(identifier));
    }

    [Theory]
    [InlineData("customers", "CUSTOMERS")]
    [InlineData(" order_id ", "ORDER_ID")]
    [InlineData("col$1#", "COL$1#")]
    [InlineData("date", "\"date\"")]
    [InlineData("Order Items", "\"Order Items\"")]
    [InlineData("1st", "\"1st\"")]
    public void FormatIdentifier_OracleLeavesRegularNonReservedIdentifiersUnquoted(string identifier, string expected)
    {
        Assert.Equal(expected, SqlDialect.For(DatabaseType.Oracle).FormatIdentifier(identifier));
    }

    [Theory]
    [InlineData("dbo.Customers", new[] { "dbo", "Customers" })]
    [InlineData("[dbo].[Order.Items]", new[] { "dbo", "Order.Items" })]
    [InlineData("\"public\".\"my table\"", new[] { "public", "my table" })]
    [InlineData("`shop`.`orders`", new[] { "shop", "orders" })]
    [InlineData(" dbo . Customers ", new[] { "dbo", "Customers" })]
    [InlineData("Customers", new[] { "Customers" })]
    [InlineData("dbo..Customers", new[] { "dbo", "Customers" })]
    public void SplitQualifiedName_SplitsOnDotsOutsideDelimiters(string qualifiedName, string[] expected)
    {
        foreach (var databaseType in Enum.GetValues<DatabaseType>())
            Assert.Equal(expected, SqlDialect.For(databaseType).SplitQualifiedName(qualifiedName));
    }

    [Theory]
    [InlineData(DatabaseType.SqlServer, "dbo.Customers", "[dbo].[Customers]")]
    [InlineData(DatabaseType.Oracle, "HR.EMPLOYEES", "\"HR\".\"EMPLOYEES\"")]
    [InlineData(DatabaseType.PostgresSql, "public.customers", "\"public\".\"customers\"")]
    [InlineData(DatabaseType.MySql, "shop.orders", "`shop`.`orders`")]
    [InlineData(DatabaseType.SqLite, "customers", "\"customers\"")]
    public void QuoteQualifiedName_QuotesEachPart(DatabaseType databaseType, string qualifiedName, string expected)
    {
        Assert.Equal(expected, SqlDialect.For(databaseType).QuoteQualifiedName(qualifiedName));
    }

    [Theory]
    [InlineData(DatabaseType.SqlServer, "@p0")]
    [InlineData(DatabaseType.Oracle, ":p0")]
    [InlineData(DatabaseType.PostgresSql, "@p0")]
    [InlineData(DatabaseType.MySql, "@p0")]
    [InlineData(DatabaseType.SqLite, "@p0")]
    public void FormatParameterReference_UsesProviderPrefix(DatabaseType databaseType, string expected)
    {
        Assert.Equal(expected, SqlDialect.For(databaseType).FormatParameterReference("p0"));
    }

    [Theory]
    [InlineData(DatabaseType.Oracle, "datadeveloper.customers", new[] { "DATADEVELOPER", "CUSTOMERS" })]
    [InlineData(DatabaseType.Oracle, "\"MinhaTabela\"", new[] { "MinhaTabela" })]
    [InlineData(DatabaseType.Oracle, "hr.\"Order Items\"", new[] { "HR", "Order Items" })]
    [InlineData(DatabaseType.PostgresSql, "Sales.Orders", new[] { "sales", "orders" })]
    [InlineData(DatabaseType.PostgresSql, "\"MixedCase\"", new[] { "MixedCase" })]
    [InlineData(DatabaseType.SqlServer, "dbo.Orders", new[] { "dbo", "Orders" })]
    [InlineData(DatabaseType.SqlServer, "[Sales].[Orders]", new[] { "Sales", "Orders" })]
    [InlineData(DatabaseType.MySql, "Shop.Orders", new[] { "Shop", "Orders" })]
    [InlineData(DatabaseType.SqLite, "Orders", new[] { "Orders" })]
    public void ResolveQualifiedName_FoldsOnlyUnquotedPartsTheWayTheDatabaseDoes(DatabaseType databaseType, string sqlName, string[] expected)
    {
        Assert.Equal(expected, SqlDialect.For(databaseType).ResolveQualifiedName(sqlName));
    }

    [Theory]
    [InlineData("MinhaTabela", "\"MinhaTabela\"")]
    [InlineData("customers", "\"customers\"")]
    [InlineData("CUSTOMERS", "CUSTOMERS")]
    [InlineData("Order Items", "Order Items")]
    [InlineData("DATE", "DATE")]
    public void ToFormattableName_OracleQuotesStoredNamesThatFormatIdentifierWouldUpperCase(string storedName, string expected)
    {
        Assert.Equal(expected, SqlDialect.For(DatabaseType.Oracle).ToFormattableName(storedName));
    }

    [Theory]
    [InlineData("MinhaTabela", "\"MinhaTabela\"")]
    [InlineData("customers", "\"customers\"")]
    [InlineData("CUSTOMERS", "CUSTOMERS")]
    [InlineData("Order Items", "\"Order Items\"")]
    [InlineData("DATE", "\"DATE\"")]
    [InlineData("date", "\"date\"")]
    public void ToFormattableName_OracleRoundTripsThroughFormatIdentifierToTheStoredName(string storedName, string expectedSql)
    {
        var dialect = SqlDialect.For(DatabaseType.Oracle);

        Assert.Equal(expectedSql, dialect.FormatIdentifier(dialect.ToFormattableName(storedName)));
    }

    [Theory]
    [InlineData(DatabaseType.SqlServer)]
    [InlineData(DatabaseType.PostgresSql)]
    [InlineData(DatabaseType.MySql)]
    [InlineData(DatabaseType.SqLite)]
    public void ToFormattableName_KeepsStoredNamesOutsideOracle(DatabaseType databaseType)
    {
        Assert.Equal("MixedCase", SqlDialect.For(databaseType).ToFormattableName("MixedCase"));
    }

    [Fact]
    public void FormatIdentifier_OracleKeepsANameTypedInQuotes()
    {
        Assert.Equal("\"MinhaTabela\"", SqlDialect.For(DatabaseType.Oracle).FormatIdentifier(" \"MinhaTabela\" "));
    }
}
