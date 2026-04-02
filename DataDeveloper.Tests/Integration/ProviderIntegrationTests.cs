using System.Data.Common;
using DataDeveloper.Data;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Models;
using Xunit;

namespace DataDeveloper.Tests.Integration;

public class ProviderIntegrationTests
{
    private static readonly TimeSpan IntegrationTimeout = TimeSpan.FromSeconds(15);

    public ProviderIntegrationTests()
    {
        DatabaseIntegrationTestSupport.EnsureDatabaseServices();
    }

    public static IEnumerable<object[]> ProviderDatabaseTypes()
    {
        yield return [DatabaseType.SqlServer];
        yield return [DatabaseType.MySql];
        yield return [DatabaseType.PostgresSql];
        yield return [DatabaseType.Oracle];
    }

    [Theory]
    [Trait("Category", "Integration")]
    [MemberData(nameof(ProviderDatabaseTypes))]
    public async Task Provider_LoadsSchemaAndExecutesSmokeQuery(DatabaseType databaseType)
    {
        if (!DatabaseIntegrationTestSupport.ShouldRunIntegrationTests())
            return;

        var connectionSettings = DatabaseIntegrationTestSupport.CreateConnectionSettings(databaseType);
        var rootConnections = await DatabaseIntegrationTestSupport.WithTimeout(
            DatabaseIntegrationTestSupport.InitializeSchemaAsync(connectionSettings),
            IntegrationTimeout,
            $"{databaseType} schema initialization");

        var root = Assert.Single(rootConnections);
        Assert.Contains(root.Children, node => node.NodeType == NodeType.Tables);
        Assert.Contains(root.Children, node => node.NodeType == NodeType.Views);

        if (databaseType != DatabaseType.SqLite)
        {
            Assert.Contains(root.Children, node => node.NodeType == NodeType.Procedures);
            Assert.Contains(root.Children, node => node.NodeType == NodeType.Functions);
        }

        var scalar = await DatabaseIntegrationTestSupport.WithTimeout(
            DatabaseIntegrationTestSupport.ExecuteScalarIntAsync(connectionSettings, DatabaseIntegrationTestSupport.GetSmokeTestQuery(databaseType)),
            IntegrationTimeout,
            $"{databaseType} smoke query");

        Assert.Equal(1, scalar);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SqLite_LoadsSchemaAndExecutesSmokeQuery()
    {
        if (!DatabaseIntegrationTestSupport.ShouldRunIntegrationTests())
            return;

        var connectionSettings = await DatabaseIntegrationTestSupport.CreateSqLiteConnectionAsync();
        var rootConnections = await DatabaseIntegrationTestSupport.WithTimeout(
            DatabaseIntegrationTestSupport.InitializeSchemaAsync(connectionSettings),
            IntegrationTimeout,
            "SQLite schema initialization");

        var root = Assert.Single(rootConnections);
        Assert.Contains(root.Children, node => node.NodeType == NodeType.Tables);
        Assert.Contains(root.Children, node => node.NodeType == NodeType.Views);
        Assert.DoesNotContain(root.Children, node => node.NodeType == NodeType.Procedures);
        Assert.DoesNotContain(root.Children, node => node.NodeType == NodeType.Functions);

        var scalar = await DatabaseIntegrationTestSupport.WithTimeout(
            DatabaseIntegrationTestSupport.ExecuteScalarIntAsync(connectionSettings, DatabaseIntegrationTestSupport.GetSmokeTestQuery(DatabaseType.SqLite)),
            IntegrationTimeout,
            "SQLite smoke query");

        Assert.Equal(1, scalar);
    }

    [Theory]
    [Trait("Category", "Integration")]
    [MemberData(nameof(ProviderDatabaseTypes))]
    public async Task Provider_LoadsExpectedSeededObjects(DatabaseType databaseType)
    {
        if (!DatabaseIntegrationTestSupport.ShouldRunIntegrationTests())
            return;

        var connectionSettings = DatabaseIntegrationTestSupport.CreateConnectionSettings(databaseType);
        var schemaExplorer = connectionSettings.GetSchemaExplorer();
        await DatabaseIntegrationTestSupport.WithTimeout(
            schemaExplorer.InitializeSchemaNode(),
            IntegrationTimeout,
            $"{databaseType} seeded object initialization");

        var root = Assert.Single(schemaExplorer.RootConnections);
        var tablesNode = root.Children.Single(node => node.NodeType == NodeType.Tables);
        var viewsNode = root.Children.Single(node => node.NodeType == NodeType.Views);
        var proceduresNode = root.Children.Single(node => node.NodeType == NodeType.Procedures);
        var functionsNode = root.Children.Single(node => node.NodeType == NodeType.Functions);

        await DatabaseIntegrationTestSupport.WithTimeout(schemaExplorer.LoadNodeAsync(tablesNode), IntegrationTimeout, $"{databaseType} tables load");
        await DatabaseIntegrationTestSupport.WithTimeout(schemaExplorer.LoadNodeAsync(viewsNode), IntegrationTimeout, $"{databaseType} views load");
        await DatabaseIntegrationTestSupport.WithTimeout(schemaExplorer.LoadNodeAsync(proceduresNode), IntegrationTimeout, $"{databaseType} procedures load");
        await DatabaseIntegrationTestSupport.WithTimeout(schemaExplorer.LoadNodeAsync(functionsNode), IntegrationTimeout, $"{databaseType} functions load");

        Assert.Contains(tablesNode.Children, node => string.Equals(node.Name, "customers", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tablesNode.Children, node => string.Equals(node.Name, "orders", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(viewsNode.Children, node => string.Equals(node.Name, "open_orders", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(proceduresNode.Children, node => NameMatches(node.Name, "mark_order_shipped"));
        Assert.Contains(functionsNode.Children, node => NameMatches(node.Name, "get_customer_total"));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SqLite_LoadsExpectedSeededObjects()
    {
        if (!DatabaseIntegrationTestSupport.ShouldRunIntegrationTests())
            return;

        var connectionSettings = await DatabaseIntegrationTestSupport.CreateSqLiteConnectionAsync();
        var schemaExplorer = connectionSettings.GetSchemaExplorer();
        await DatabaseIntegrationTestSupport.WithTimeout(
            schemaExplorer.InitializeSchemaNode(),
            IntegrationTimeout,
            "SQLite seeded object initialization");

        var root = Assert.Single(schemaExplorer.RootConnections);
        var tablesNode = root.Children.Single(node => node.NodeType == NodeType.Tables);
        var viewsNode = root.Children.Single(node => node.NodeType == NodeType.Views);

        Assert.Contains(tablesNode.Children, node => string.Equals(node.Name, "customers", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tablesNode.Children, node => string.Equals(node.Name, "orders", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(viewsNode.Children, node => string.Equals(node.Name, "open_orders", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [Trait("Category", "Integration")]
    [MemberData(nameof(ProviderDatabaseTypes))]
    public async Task Provider_LoadsColumnsAndRoutineParameters(DatabaseType databaseType)
    {
        if (!DatabaseIntegrationTestSupport.ShouldRunIntegrationTests())
            return;

        var connectionSettings = DatabaseIntegrationTestSupport.CreateConnectionSettings(databaseType);
        var schemaExplorer = connectionSettings.GetSchemaExplorer();
        await DatabaseIntegrationTestSupport.WithTimeout(
            schemaExplorer.InitializeSchemaNode(),
            IntegrationTimeout,
            $"{databaseType} schema load");

        var root = Assert.Single(schemaExplorer.RootConnections);
        var tablesNode = root.Children.Single(node => node.NodeType == NodeType.Tables);
        var proceduresNode = root.Children.Single(node => node.NodeType == NodeType.Procedures);
        var functionsNode = root.Children.Single(node => node.NodeType == NodeType.Functions);

        await DatabaseIntegrationTestSupport.WithTimeout(schemaExplorer.LoadNodeAsync(tablesNode), IntegrationTimeout, $"{databaseType} tables load");
        await DatabaseIntegrationTestSupport.WithTimeout(schemaExplorer.LoadNodeAsync(proceduresNode), IntegrationTimeout, $"{databaseType} procedures load");
        await DatabaseIntegrationTestSupport.WithTimeout(schemaExplorer.LoadNodeAsync(functionsNode), IntegrationTimeout, $"{databaseType} functions load");

        var ordersNode = Assert.Single(tablesNode.Children, node => string.Equals(node.Name, "orders", StringComparison.OrdinalIgnoreCase));
        var ordersColumnsNode = Assert.Single(ordersNode.Children, node => node.NodeType == NodeType.Columns);
        await DatabaseIntegrationTestSupport.WithTimeout(schemaExplorer.LoadNodeAsync(ordersColumnsNode), IntegrationTimeout, $"{databaseType} order columns load");
        Assert.Contains(ordersColumnsNode.Children, node => string.Equals(node.Name, "order_id", StringComparison.OrdinalIgnoreCase));

        var procedureNode = Assert.Single(proceduresNode.Children, node => NameMatches(node.Name, "mark_order_shipped"));
        var procedureParametersNode = Assert.Single(procedureNode.Children, node => node.NodeType == NodeType.Parameters);
        await DatabaseIntegrationTestSupport.WithTimeout(schemaExplorer.LoadNodeAsync(procedureParametersNode), IntegrationTimeout, $"{databaseType} procedure parameter load");
        Assert.NotEmpty(procedureParametersNode.Children);

        var functionNode = Assert.Single(functionsNode.Children, node => NameMatches(node.Name, "get_customer_total"));
        var functionParametersNode = Assert.Single(functionNode.Children, node => node.NodeType == NodeType.Parameters);
        await DatabaseIntegrationTestSupport.WithTimeout(schemaExplorer.LoadNodeAsync(functionParametersNode), IntegrationTimeout, $"{databaseType} function parameter load");
        Assert.NotEmpty(functionParametersNode.Children);
    }

    [Theory]
    [Trait("Category", "Integration")]
    [MemberData(nameof(ProviderDatabaseTypes))]
    public async Task Provider_ExecutesSeededFunction(DatabaseType databaseType)
    {
        if (!DatabaseIntegrationTestSupport.ShouldRunIntegrationTests())
            return;

        var connectionSettings = DatabaseIntegrationTestSupport.CreateConnectionSettings(databaseType);
        var sql = databaseType switch
        {
            DatabaseType.SqlServer => "select dbo.get_customer_total(@customer_id);",
            DatabaseType.MySql => "select get_customer_total(@customer_id);",
            DatabaseType.PostgresSql => "select \"get_customer_total\"(@customer_id);",
            DatabaseType.Oracle => "select get_customer_total(:customer_id) from dual",
            _ => throw new NotSupportedException()
        };

        var executor = connectionSettings.GetStatementExecutor();
        var results = (await DatabaseIntegrationTestSupport.WithTimeout(
            executor.ExecuteStatement(sql, new Dictionary<string, object?> { ["@customer_id"] = 1 }),
            IntegrationTimeout,
            $"{databaseType} function execution")).ToList();

        var result = Assert.Single(results);
        var reader = Assert.IsAssignableFrom<DbDataReader>(result.DataReader);
        try
        {
            Assert.True(await reader.ReadAsync());
            Assert.True(Convert.ToDecimal(reader.GetValue(0)) > 0m);
        }
        finally
        {
            await result.CloseDataReader();
        }
    }

    private static bool NameMatches(string actualName, string expectedName)
    {
        return string.Equals(actualName, expectedName, StringComparison.OrdinalIgnoreCase) ||
               actualName.EndsWith("." + expectedName, StringComparison.OrdinalIgnoreCase);
    }
}
