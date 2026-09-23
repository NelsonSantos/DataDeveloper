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
    public async Task LoadNodeAsync_FillsKeysConstraintsAndIndexesOfATable()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"datadeveloper-explorer-{Guid.NewGuid():N}.db");
        try
        {
            using (var setup = new SqliteConnection($"Data Source={databasePath}"))
            {
                setup.Open();
                using var command = setup.CreateCommand();
                command.CommandText = """
                                      create table customers (id integer primary key, email text not null, unique (email));
                                      create table orders (
                                          id integer primary key,
                                          customer_id integer not null references customers (id),
                                          total real not null check (total >= 0),
                                          code text,
                                          constraint ck_code check (length(code) = 8));
                                      create index ix_orders_customer on orders (customer_id, total desc);
                                      create unique index ux_orders_code on orders (code);
                                      """;
                command.ExecuteNonQuery();
            }

            var settings = new SqLiteConnectionSettings { Name = "Local", DatabaseType = DatabaseType.SqLite, Database = databasePath };
            var explorer = new SchemaExplorer(new SqLiteDatabaseProvider(settings), settings);
            await explorer.InitializeSchemaNode();

            var tables = explorer.RootConnections[0].Children.Single(node => node.NodeType == NodeType.Tables).Children;
            var orders = tables.Single(node => node.Name == "orders");
            Assert.Equal(
                [NodeType.Columns, NodeType.Keys, NodeType.Constraints, NodeType.Indexes],
                orders.Children.Select(node => node.NodeType));

            var keys = await LoadFolderAsync(explorer, orders, NodeType.Keys);
            Assert.Collection(
                keys,
                node => { Assert.Equal(NodeType.PrimaryKey, node.NodeType); Assert.Equal("PRIMARY KEY", node.Name); Assert.Equal("(id)", node.Details); },
                node => { Assert.Equal(NodeType.ForeignKey, node.NodeType); Assert.Equal("(customer_id) → customers (id)", node.Details); });

            var checks = await LoadFolderAsync(explorer, orders, NodeType.Constraints);
            Assert.Collection(
                checks,
                node => { Assert.Equal(NodeType.CheckConstraint, node.NodeType); Assert.Equal("CHECK", node.Name); Assert.Equal("total >= 0", node.Details); },
                node => { Assert.Equal("ck_code", node.Name); Assert.Equal("length(code) = 8", node.Details); });

            var indexes = await LoadFolderAsync(explorer, orders, NodeType.Indexes);
            Assert.Collection(
                indexes,
                node => { Assert.Equal(NodeType.Index, node.NodeType); Assert.Equal("ix_orders_customer", node.Name); Assert.Equal("(customer_id, total desc)", node.Details); },
                node => { Assert.Equal("ux_orders_code", node.Name); Assert.Equal("unique (code)", node.Details); });

            // A UNIQUE constraint is a key, not an index.
            var customers = tables.Single(node => node.Name == "customers");
            var customerKeys = await LoadFolderAsync(explorer, customers, NodeType.Keys);
            var uniqueKey = Assert.Single(customerKeys, node => node.NodeType == NodeType.UniqueKey);
            Assert.Equal("(email)", uniqueKey.Details);
            Assert.Empty(await LoadFolderAsync(explorer, customers, NodeType.Indexes));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task RefreshSchemaObjectAsync_AfterAlterTable_ReloadsOnlyTheOpenedFolders()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"datadeveloper-explorer-{Guid.NewGuid():N}.db");
        try
        {
            var settings = new SqLiteConnectionSettings { Name = "Local", DatabaseType = DatabaseType.SqLite, Database = databasePath };
            var alterStatement = "alter table orders add column note text check (length(note) < 10)";
            using (var setup = new SqliteConnection($"Data Source={databasePath}"))
            {
                setup.Open();
                using var command = setup.CreateCommand();
                command.CommandText = "create table orders (id integer primary key)";
                command.ExecuteNonQuery();
            }

            var explorer = new SchemaExplorer(new SqLiteDatabaseProvider(settings), settings);
            await explorer.InitializeSchemaNode();
            var orders = explorer.RootConnections[0].Children.Single(node => node.NodeType == NodeType.Tables).Children.Single();
            Assert.Empty(await LoadFolderAsync(explorer, orders, NodeType.Constraints));

            using (var alter = new SqliteConnection($"Data Source={databasePath}"))
            {
                alter.Open();
                using var command = alter.CreateCommand();
                command.CommandText = alterStatement;
                command.ExecuteNonQuery();
            }

            await explorer.RefreshSchemaObjectAsync(alterStatement);

            var constraints = orders.Children.Single(node => node.NodeType == NodeType.Constraints);
            Assert.Equal("length(note) < 10", Assert.Single(constraints.Children).Details);
            Assert.True(orders.Children.Single(node => node.NodeType == NodeType.Indexes).CanLoad);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(databasePath);
        }
    }

    private static async Task<IReadOnlyList<SchemaNode>> LoadFolderAsync(SchemaExplorer explorer, SchemaNode table, NodeType folderType)
    {
        var folder = table.Children.Single(node => node.NodeType == folderType);
        await explorer.LoadNodeAsync(folder);
        Assert.False(folder.CanLoad);
        return folder.Children.ToList();
    }
}
