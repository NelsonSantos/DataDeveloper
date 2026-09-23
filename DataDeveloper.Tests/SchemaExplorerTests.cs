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
                [NodeType.Columns, NodeType.Keys, NodeType.Constraints, NodeType.Indexes, NodeType.Triggers],
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

    [Fact]
    public async Task Triggers_AreListedWithTheirDdlAndReloadedAfterTriggerDdl()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"datadeveloper-explorer-{Guid.NewGuid():N}.db");
        try
        {
            var settings = new SqLiteConnectionSettings { Name = "Local", DatabaseType = DatabaseType.SqLite, Database = databasePath };
            Execute(databasePath, """
                                  create table orders (id integer primary key, total real, updated_at text);
                                  create table audit (order_id integer);
                                  create trigger trg_orders_audit after insert on orders begin insert into audit values (new.id); end;
                                  create trigger trg_orders_touch update of total on orders begin update orders set updated_at = 'now' where id = new.id; end;
                                  """);

            var explorer = new SchemaExplorer(new SqLiteDatabaseProvider(settings), settings);
            await explorer.InitializeSchemaNode();
            var orders = explorer.RootConnections[0].Children.Single(node => node.NodeType == NodeType.Tables)
                .Children.Single(node => node.Name == "orders");

            var triggers = await LoadFolderAsync(explorer, orders, NodeType.Triggers);
            Assert.Collection(
                triggers,
                node => { Assert.Equal(NodeType.Trigger, node.NodeType); Assert.Equal("trg_orders_audit", node.Name); Assert.Equal("after insert", node.Details); },
                node => { Assert.Equal("trg_orders_touch", node.Name); Assert.Equal("before update", node.Details); });
            Assert.Equal(
                new DbObjectRef(DbObjectKind.Trigger, "main", "trg_orders_audit", new DbObjectRef(DbObjectKind.Table, "main", "orders")),
                triggers[0].ObjectRef);

            var ddl = await new Data.Services.Metadata.SchemaMetadataService(settings, new SqLiteDatabaseProvider(settings), catalog: null).GetDdlAsync(triggers[0]);
            Assert.StartsWith("CREATE TRIGGER trg_orders_audit after insert on orders", ddl, StringComparison.OrdinalIgnoreCase);

            // DROP TRIGGER does not name the table, so every opened Triggers folder is reloaded.
            const string dropStatement = "drop trigger trg_orders_audit";
            Execute(databasePath, dropStatement);
            await explorer.RefreshSchemaObjectAsync(dropStatement);

            var reloaded = orders.Children.Single(node => node.NodeType == NodeType.Triggers).Children;
            Assert.Equal("trg_orders_touch", Assert.Single(reloaded).Name);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task RefreshSchemaObjectAsync_AfterCreateIndex_ReloadsOpenedKeysAndIndexesFolders()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"datadeveloper-explorer-{Guid.NewGuid():N}.db");
        try
        {
            var settings = new SqLiteConnectionSettings { Name = "Local", DatabaseType = DatabaseType.SqLite, Database = databasePath };
            Execute(databasePath, "create table orders (id integer primary key, code text)");

            var explorer = new SchemaExplorer(new SqLiteDatabaseProvider(settings), settings);
            await explorer.InitializeSchemaNode();
            var orders = explorer.RootConnections[0].Children.Single(node => node.NodeType == NodeType.Tables).Children.Single();
            Assert.Empty(await LoadFolderAsync(explorer, orders, NodeType.Indexes));

            const string createStatement = "create unique index ux_orders_code on orders (code)";
            Execute(databasePath, createStatement);
            await explorer.RefreshSchemaObjectAsync(createStatement);

            var indexes = orders.Children.Single(node => node.NodeType == NodeType.Indexes).Children;
            Assert.Equal("ux_orders_code", Assert.Single(indexes).Name);
            Assert.True(orders.Children.Single(node => node.NodeType == NodeType.Triggers).CanLoad);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(databasePath);
        }
    }

    private static void Execute(string databasePath, string sql)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static async Task<IReadOnlyList<SchemaNode>> LoadFolderAsync(SchemaExplorer explorer, SchemaNode table, NodeType folderType)
    {
        var folder = table.Children.Single(node => node.NodeType == folderType);
        await explorer.LoadNodeAsync(folder);
        Assert.False(folder.CanLoad);
        return folder.Children.ToList();
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

    [Fact]
    public async Task SchemaRefreshed_IsRaisedAfterInitializeAndAfterObjectRefresh()
    {
        var connection = new SqlServerConnectionSettings { DatabaseType = DatabaseType.SqlServer, Name = "Test" };
        var explorer = new SchemaExplorer(new FakeDatabaseProvider(), connection, new FakeObjectCatalog("orders"));
        var raised = 0;
        explorer.SchemaRefreshed += (_, _) => raised++;

        await explorer.InitializeSchemaNode();
        Assert.Equal(1, raised);

        await explorer.RefreshSchemaObjectAsync("create table dbo.new_table (id int)");
        Assert.Equal(2, raised);
    }
}
