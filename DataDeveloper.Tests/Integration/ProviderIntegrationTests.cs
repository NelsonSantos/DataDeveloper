using DataDeveloper.Data;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Models;
using Dapper;
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
        var reader = Assert.IsAssignableFrom<System.Data.Common.DbDataReader>(results[0].DataReader);
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
        var reader = Assert.IsAssignableFrom<System.Data.Common.DbDataReader>(results[0].DataReader);
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1, Convert.ToInt32(reader.GetValue(0)));
        await results[0].CloseDataReader();
    }

    [Fact]
    public async Task SqlServerConnection_ExecStoredProcedure_ReturnsMultipleResultSets()
    {
        if (!DatabaseIntegrationTestSupport.ShouldRunIntegrationTests())
            return;

        var connectionSettings = DatabaseIntegrationTestSupport.TryLoadConnection("NassServer");
        if (connectionSettings is null)
            return;

        const string procedureName = "dbo.DataDeveloper_MultiResult_Test";

        await using (var connection = connectionSettings.GetDatabaseProvider().GetConnection())
        {
            await connection.ExecuteAsync($"""
                                           create or alter procedure {procedureName}
                                           as
                                           begin
                                               set nocount on;
                                               select 1 as a;
                                               select 2 as b;
                                           end
                                           """);
        }

        var statementExecutor = connectionSettings.GetStatementExecutor();
        var results = (await DatabaseIntegrationTestSupport.WithTimeout(
            statementExecutor.ExecuteStatement($"exec {procedureName}"),
            IntegrationTimeout,
            "SQL Server stored procedure multi-result execution")).ToList();

        Assert.Equal(2, results.Count(result => result.HasDataReader));

        foreach (var result in results)
            await result.CloseDataReader();
    }

    [Fact]
    public async Task SqlServerConnection_ExecUserProcedure_ReturnsTwoResultTabs()
    {
        if (!DatabaseIntegrationTestSupport.ShouldRunIntegrationTests())
            return;

        var connectionSettings = DatabaseIntegrationTestSupport.TryLoadConnection("NassServer");
        if (connectionSettings is null)
            return;

        var statementExecutor = connectionSettings.GetStatementExecutor();
        var results = (await DatabaseIntegrationTestSupport.WithTimeout(
            statementExecutor.ExecuteStatement("exec test_two_resultsets;"),
            IntegrationTimeout,
            "SQL Server user procedure multi-result execution")).ToList();

        var dataResults = results.Where(result => result.HasDataReader).ToList();

        Assert.Equal(2, dataResults.Count);

        foreach (var result in dataResults)
        {
            var reader = Assert.IsAssignableFrom<System.Data.Common.DbDataReader>(result.DataReader);
            Assert.True(await reader.ReadAsync());
        }

        foreach (var result in results)
            await result.CloseDataReader();
    }

}
