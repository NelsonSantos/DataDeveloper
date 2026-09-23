using System.Data.Common;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Providers.SqLite;
using DataDeveloper.Data.Providers.SqlServer;
using DataDeveloper.Data.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DataDeveloper.Tests;

public class SchemaExplorerTests
{
    private sealed class FakeDatabaseProvider : IDatabaseProvider
    {
        public DbConnection GetConnection()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            return connection;
        }

        public TestConnectionResult TestConnection() => new(true, "ok");
        public IReadOnlyList<string> GetAvailableDatabaseNames() => [];
    }

    [Fact]
    public async Task InitializeSchemaNode_UsesConnectionNameWithDatabase_ForRootNode()
    {
        var connection = new SqlServerConnectionSettings
        {
            DatabaseType = DatabaseType.SqlServer,
            Name = "Oreons-Backoffice",
            Server = "localhost",
            Database = "NXGenMarketplace_Backoffice_Development"
        };
        var explorer = new SchemaExplorer(new FakeDatabaseProvider(), connection, new FakeObjectCatalog());

        await explorer.InitializeSchemaNode();

        var root = Assert.Single(explorer.RootConnections);
        Assert.Equal(
            "Oreons-Backoffice (NXGenMarketplace_Backoffice_Development)",
            root.Name);
    }

    [Fact]
    public async Task InitializeSchemaNode_BuildsFoldersFromTheProviderCatalog()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"datadeveloper-explorer-{Guid.NewGuid():N}.db");
        try
        {
            using (var setup = new SqliteConnection($"Data Source={databasePath}"))
            {
                setup.Open();
                using var command = setup.CreateCommand();
                command.CommandText = """
                                      create table orders (id integer primary key, total real not null);
                                      create view open_orders as select id from orders;
                                      """;
                command.ExecuteNonQuery();
            }

            var settings = new SqLiteConnectionSettings { Name = "Local", DatabaseType = DatabaseType.SqLite, Database = databasePath };
            var explorer = new SchemaExplorer(new SqLiteDatabaseProvider(settings), settings);

            await explorer.InitializeSchemaNode();

            var root = Assert.Single(explorer.RootConnections);
            // SQLite has no routines, so its catalog exposes only tables and views.
            Assert.Equal([NodeType.Tables, NodeType.Views], root.Children.Select(node => node.NodeType));

            var ordersNode = Assert.Single(root.Children[0].Children);
            Assert.Equal("orders", ordersNode.Name);
            Assert.Equal(new DbObjectRef(DbObjectKind.Table, "main", "orders"), ordersNode.ObjectRef);
            Assert.Equal(new DbObjectRef(DbObjectKind.View, "main", "open_orders"), Assert.Single(root.Children[1].Children).ObjectRef);

            var columnsFolder = ordersNode.Children.Single(node => node.NodeType == NodeType.Columns);
            await explorer.LoadNodeAsync(columnsFolder);
            Assert.Equal(["id", "total"], columnsFolder.Children.Select(node => node.Name));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task LoadNodeAsync_WhenTheLoadFails_ResetsTheFolderSoExpandingAgainRetries()
    {
        var connection = new SqlServerConnectionSettings { DatabaseType = DatabaseType.SqlServer, Name = "Test" };
        var catalog = new FakeObjectCatalog("orders") { ColumnsStatement = "select * from no_such_table" };
        var explorer = new SchemaExplorer(new FakeDatabaseProvider(), connection, catalog);
        await explorer.InitializeSchemaNode();
        var columns = explorer.RootConnections[0].Children.Single(node => node.NodeType == NodeType.Tables)
            .Children.Single().Children.Single(node => node.NodeType == NodeType.Columns);
        columns.IsExpanded = true;

        await Assert.ThrowsAsync<SqliteException>(() => explorer.LoadNodeAsync(columns));

        Assert.True(columns.CanLoad);
        Assert.False(columns.IsExpanded);
        Assert.Equal(NodeType.None, Assert.Single(columns.Children).NodeType);

        catalog.ColumnsStatement = "select 'id' as Name, 'int' as DataType";
        await explorer.LoadNodeAsync(columns);

        Assert.False(columns.CanLoad);
        Assert.Equal("id", Assert.Single(columns.Children).Name);
    }
}
