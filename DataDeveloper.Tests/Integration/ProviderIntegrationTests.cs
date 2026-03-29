using DataDeveloper.Data;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Models;
using Xunit;

namespace DataDeveloper.Tests.Integration;

public class ProviderIntegrationTests
{
    private static readonly TimeSpan IntegrationTimeout = TimeSpan.FromSeconds(10);

    public ProviderIntegrationTests()
    {
        DatabaseIntegrationTestSupport.EnsureDatabaseServices();
    }

    [Fact]
    public async Task SqlServerConnection_LoadsSchemaAndExecutesQuery()
    {
        if (!DatabaseIntegrationTestSupport.ShouldRunIntegrationTests())
            return;

        var connection = DatabaseIntegrationTestSupport.TryLoadConnection("NassServer");
        if (connection is null)
            return;

        Assert.Equal(DatabaseType.SqlServer, connection.DatabaseType);

        var schemaExplorer = connection.GetSchemaExplorer();
        await DatabaseIntegrationTestSupport.WithTimeout(
            schemaExplorer.InitializeSchemaNode(),
            IntegrationTimeout,
            "SQL Server schema initialization");

        var tablesNode = schemaExplorer.RootConnections
            .SelectMany(root => root.Children)
            .FirstOrDefault(node => node.NodeType == NodeType.Tables);

        Assert.NotNull(tablesNode);
        Assert.NotEmpty(tablesNode!.Children);

        var statementExecutor = connection.GetStatementExecutor();
        var results = (await DatabaseIntegrationTestSupport.WithTimeout(
            statementExecutor.ExecuteStatement("select 1 as value"),
            IntegrationTimeout,
            "SQL Server statement execution")).ToList();

        Assert.Single(results);
        var reader = results[0].DataReader;
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1, Convert.ToInt32(reader.GetValue(0)));
        await results[0].CloseDataReader();
    }

    [Fact]
    public async Task MySqlConnection_LoadsSchemaAndExecutesQuery()
    {
        if (!DatabaseIntegrationTestSupport.ShouldRunIntegrationTests())
            return;

        var connection = DatabaseIntegrationTestSupport.TryLoadConnection("repres nass-server");
        if (connection is null)
            return;

        Assert.Equal(DatabaseType.MySql, connection.DatabaseType);

        var schemaExplorer = connection.GetSchemaExplorer();
        await DatabaseIntegrationTestSupport.WithTimeout(
            schemaExplorer.InitializeSchemaNode(),
            IntegrationTimeout,
            "MySQL schema initialization");

        var tablesNode = schemaExplorer.RootConnections
            .SelectMany(root => root.Children)
            .FirstOrDefault(node => node.NodeType == NodeType.Tables);

        Assert.NotNull(tablesNode);
        Assert.NotEmpty(tablesNode!.Children);

        var statementExecutor = connection.GetStatementExecutor();
        var results = (await DatabaseIntegrationTestSupport.WithTimeout(
            statementExecutor.ExecuteStatement("select 1 as value"),
            IntegrationTimeout,
            "MySQL statement execution")).ToList();

        Assert.Single(results);
        var reader = results[0].DataReader;
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1, Convert.ToInt32(reader.GetValue(0)));
        await results[0].CloseDataReader();
    }
}
