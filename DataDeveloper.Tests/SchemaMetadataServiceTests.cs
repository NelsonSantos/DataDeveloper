using DataDeveloper.Data;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Providers.SqLite;
using DataDeveloper.Data.Services;
using DataDeveloper.Data.Services.Metadata;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DataDeveloper.Tests;

public sealed class SchemaMetadataServiceTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"datadeveloper-metadata-{Guid.NewGuid():N}.db");
    private readonly SchemaMetadataService _service;

    public SchemaMetadataServiceTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton<DatabaseProviderFactoryService>();
        DatabaseExtensionsMethods.SetServiceProvider(services.BuildServiceProvider());

        using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                                  create table orders (id integer primary key, total real not null);
                                  create view open_orders as select id from orders where total > 0;
                                  create table order_items (
                                      order_id integer not null references orders (id) on delete cascade,
                                      line_no integer not null,
                                      quantity integer not null default 1,
                                      primary key (order_id, line_no));
                                  create index ix_order_items_quantity on order_items (quantity desc);
                                  """;
            command.ExecuteNonQuery();
        }

        _service = new SchemaMetadataService(new SqLiteConnectionSettings
        {
            Id = Guid.NewGuid(),
            Name = "metadata",
            DatabaseType = DatabaseType.SqLite,
            Database = _databasePath
        });
    }

    [Fact]
    public async Task GetDdlAsync_ReturnsTableDefinition()
    {
        var ddl = await _service.GetDdlAsync(new DbObjectRef(DbObjectKind.Table, null, "orders"));

        Assert.Equal("CREATE TABLE orders (id integer primary key, total real not null)", ddl);
    }

    [Fact]
    public async Task GetDdlAsync_ReturnsViewDefinition()
    {
        var ddl = await _service.GetDdlAsync(new DbObjectRef(DbObjectKind.View, null, "open_orders"));

        Assert.Equal("CREATE VIEW open_orders as select id from orders where total > 0", ddl);
    }

    [Fact]
    public async Task GetDdlAsync_ReturnsEmptyForMissingObject()
    {
        Assert.Equal(string.Empty, await _service.GetDdlAsync(new DbObjectRef(DbObjectKind.Table, null, "missing")));
    }

    [Fact]
    public async Task GetDdlAsync_ReturnsEmptyWhenProviderHasNoSuchObjectKind()
    {
        Assert.Equal(string.Empty, await _service.GetDdlAsync(new DbObjectRef(DbObjectKind.Procedure, null, "orders")));
    }

    [Fact]
    public async Task GetDdlAsync_FromSchemaNode_ReadsTheNodeObject()
    {
        var node = CreateNode(NodeType.Table, "orders");

        var ddl = await _service.GetDdlAsync(node);

        Assert.StartsWith("CREATE TABLE orders", ddl, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetDdlAsync_FromNonObjectNode_ReturnsEmpty()
    {
        var node = CreateNode(NodeType.Tables, "Tables");

        Assert.Equal(string.Empty, await _service.GetDdlAsync(node));
    }

    [Fact]
    public async Task GetTableStructureAsync_ReadsDefaultsKeysAndIndexes()
    {
        var structure = await _service.GetTableStructureAsync(new DbObjectRef(DbObjectKind.Table, null, "order_items"));

        Assert.Equal("1", Assert.Single(structure.ColumnDefaults, row => row.ColumnName == "quantity").DefaultValueExpression);
        Assert.Equal(["order_id", "line_no"], structure.PrimaryKeyColumns.OrderBy(row => row.OrdinalPosition).Select(row => row.ColumnName));

        var foreignKey = Assert.Single(structure.ForeignKeyColumns);
        Assert.Equal("order_id", foreignKey.ColumnName);
        Assert.Equal("orders", foreignKey.ReferencedTableName);
        Assert.Equal("id", foreignKey.ReferencedColumnName);
        Assert.Equal("cascade", foreignKey.OnDeleteAction);

        var index = Assert.Single(structure.IndexColumns);
        Assert.Equal("ix_order_items_quantity", index.IndexName);
        Assert.Equal("quantity", index.ColumnName);
        Assert.True(index.IsDescending);
    }

    [Fact]
    public async Task GetTableStructureAsync_ReturnsEmptyForMissingTable()
    {
        var structure = await _service.GetTableStructureAsync(new DbObjectRef(DbObjectKind.Table, null, "missing"));

        Assert.Empty(structure.ColumnDefaults);
        Assert.Empty(structure.PrimaryKeyColumns);
        Assert.Empty(structure.ForeignKeyColumns);
        Assert.Empty(structure.IndexColumns);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_databasePath))
            File.Delete(_databasePath);
    }

    private static SchemaNode CreateNode(NodeType nodeType, string name)
    {
        return (SchemaNode)Activator.CreateInstance(
                   typeof(SchemaNode),
                   System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                   binder: null,
                   args: [nodeType, name, false, null, false, null, null],
                   culture: null)!;
    }
}
