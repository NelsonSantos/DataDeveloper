using System.Data.Common;
using DataDeveloper.Data;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Models.SchemaCompare;
using DataDeveloper.Data.Services.Metadata;
using DataDeveloper.Data.Services.SchemaCompare;
using DataDeveloper.Services.SchemaCompare;
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

    [Theory]
    [Trait("Category", "Integration")]
    [MemberData(nameof(ProviderDatabaseTypes))]
    public async Task Provider_ReadsNativeDdlForSeededObjects(DatabaseType databaseType)
    {
        if (!DatabaseIntegrationTestSupport.ShouldRunIntegrationTests())
            return;

        var connectionSettings = DatabaseIntegrationTestSupport.CreateConnectionSettings(databaseType);
        var schemaExplorer = connectionSettings.GetSchemaExplorer();
        await DatabaseIntegrationTestSupport.WithTimeout(
            schemaExplorer.InitializeSchemaNode(),
            IntegrationTimeout,
            $"{databaseType} DDL object initialization");

        var root = Assert.Single(schemaExplorer.RootConnections);
        var metadataService = new SchemaMetadataService(connectionSettings);

        foreach (var (folderType, objectName) in new[]
                 {
                     (NodeType.Tables, "orders"),
                     (NodeType.Views, "open_orders"),
                     (NodeType.Procedures, "mark_order_shipped"),
                     (NodeType.Functions, "get_customer_total")
                 })
        {
            var folder = root.Children.Single(node => node.NodeType == folderType);
            var objectNode = folder.Children.Single(node => NameMatches(node.Name, objectName));

            var ddl = await DatabaseIntegrationTestSupport.WithTimeout(
                metadataService.GetDdlAsync(objectNode),
                IntegrationTimeout,
                $"{databaseType} {objectName} DDL");

            // The seeded MySQL routines are defined by root, and the integration account cannot
            // see their bodies, so SHOW CREATE returns no DDL for them.
            if (databaseType == DatabaseType.MySql && folderType is NodeType.Procedures or NodeType.Functions)
            {
                Assert.Equal(string.Empty, ddl);
                continue;
            }

            Assert.Contains(objectName, ddl, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("create", ddl, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [Trait("Category", "Integration")]
    [MemberData(nameof(ProviderDatabaseTypes))]
    public async Task Provider_SchemaCompareOfViewsAndRoutinesAgainstItself(DatabaseType databaseType)
    {
        if (!DatabaseIntegrationTestSupport.ShouldRunIntegrationTests())
            return;

        var connectionSettings = DatabaseIntegrationTestSupport.CreateConnectionSettings(databaseType);
        var objects = await DatabaseIntegrationTestSupport.WithTimeout(
            SchemaCompareObjectEnumerator.EnumerateAsync(connectionSettings),
            IntegrationTimeout,
            $"{databaseType} compare object enumeration");
        var selected = objects
            .Where(item => NameMatches(item.Name, "open_orders") ||
                           NameMatches(item.Name, "mark_order_shipped") ||
                           NameMatches(item.Name, "get_customer_total"))
            .ToList();
        Assert.Equal(3, selected.Count);

        var results = await DatabaseIntegrationTestSupport.WithTimeout(
            SchemaCompareEngine.CompareAsync(connectionSettings, connectionSettings, selected),
            IntegrationTimeout,
            $"{databaseType} schema compare");

        foreach (var result in results.Where(item => selected.Any(objectRef => objectRef.Name == item.Name)))
        {
            // The integration account cannot read the bodies of the root-owned MySQL routines,
            // which must be reported instead of silently comparing as unchanged.
            if (databaseType == DatabaseType.MySql && result.ObjectType != SchemaCompareObjectType.View)
            {
                Assert.Equal(SchemaCompareResultStatus.Error, result.Status);
                Assert.Contains("permission", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
                continue;
            }

            Assert.Equal(SchemaCompareResultStatus.Unchanged, result.Status);
        }
    }

    [Theory]
    [Trait("Category", "Integration")]
    [MemberData(nameof(ProviderDatabaseTypes))]
    public async Task Provider_TreeNodesCarryTheirSchemaAndLoadColumns(DatabaseType databaseType)
    {
        if (!DatabaseIntegrationTestSupport.ShouldRunIntegrationTests())
            return;

        var connectionSettings = DatabaseIntegrationTestSupport.CreateConnectionSettings(databaseType);
        var schemaExplorer = connectionSettings.GetSchemaExplorer();
        await DatabaseIntegrationTestSupport.WithTimeout(
            schemaExplorer.InitializeSchemaNode(),
            IntegrationTimeout,
            $"{databaseType} schema initialization");

        var root = Assert.Single(schemaExplorer.RootConnections);
        var defaultSchema = databaseType switch
        {
            DatabaseType.SqlServer => "dbo",
            DatabaseType.MySql => "datadeveloper",
            DatabaseType.PostgresSql => "public",
            _ => "DATADEVELOPER"
        };

        // Objects in the default schema are shown unqualified, routines included.
        var ordersNode = root.Children.Single(node => node.NodeType == NodeType.Tables).Children
            .Single(node => string.Equals(node.Name, "orders", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(ordersNode.ObjectRef);
        Assert.Equal(DbObjectKind.Table, ordersNode.ObjectRef!.Kind);
        Assert.Equal(defaultSchema, ordersNode.ObjectRef.Schema);

        var procedureNode = root.Children.Single(node => node.NodeType == NodeType.Procedures).Children
            .Single(node => NameMatches(node.Name, "mark_order_shipped"));
        Assert.Equal("mark_order_shipped", procedureNode.Name, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(defaultSchema, procedureNode.ObjectRef!.Schema);

        var columnsFolder = ordersNode.Children.Single(node => node.NodeType == NodeType.Columns);
        await DatabaseIntegrationTestSupport.WithTimeout(schemaExplorer.LoadNodeAsync(columnsFolder), IntegrationTimeout, $"{databaseType} orders columns");
        Assert.Contains(columnsFolder.Children, node => string.Equals(node.Name, "order_total", StringComparison.OrdinalIgnoreCase));

        var parametersFolder = procedureNode.Children.Single(node => node.NodeType == NodeType.Parameters);
        await DatabaseIntegrationTestSupport.WithTimeout(schemaExplorer.LoadNodeAsync(parametersFolder), IntegrationTimeout, $"{databaseType} procedure parameters");
        Assert.NotEmpty(parametersFolder.Children);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SqlServer_TableOutsideDefaultSchema_IsQualifiedInTreeAndUsableEverywhere()
    {
        if (!DatabaseIntegrationTestSupport.ShouldRunIntegrationTests())
            return;

        var connectionSettings = DatabaseIntegrationTestSupport.CreateConnectionSettings(DatabaseType.SqlServer);
        var token = Guid.NewGuid().ToString("N")[..8];
        var schemaName = $"tds_{token}";

        await DatabaseIntegrationTestSupport.ExecuteNonQueryAsync(connectionSettings, $"create schema {schemaName}");
        try
        {
            // Same table name as the seeded dbo.orders, so an unqualified lookup would find the wrong one.
            await DatabaseIntegrationTestSupport.ExecuteNonQueryAsync(
                connectionSettings,
                $"create table {schemaName}.orders (other_id int not null constraint pk_{schemaName}_orders primary key, note varchar(20) null)");

            var schemaExplorer = connectionSettings.GetSchemaExplorer();
            await DatabaseIntegrationTestSupport.WithTimeout(schemaExplorer.InitializeSchemaNode(), IntegrationTimeout, "SQL Server schema initialization");

            var tables = Assert.Single(schemaExplorer.RootConnections).Children.Single(node => node.NodeType == NodeType.Tables).Children;
            Assert.Contains(tables, node => node.Name == "orders");
            var otherOrders = Assert.Single(tables, node => node.Name == $"{schemaName}.orders");
            Assert.Equal(new DbObjectRef(DbObjectKind.Table, schemaName, "orders"), otherOrders.ObjectRef);

            var columnsFolder = otherOrders.Children.Single(node => node.NodeType == NodeType.Columns);
            await DatabaseIntegrationTestSupport.WithTimeout(schemaExplorer.LoadNodeAsync(columnsFolder), IntegrationTimeout, "SQL Server columns");
            Assert.Equal(["other_id", "note"], columnsFolder.Children.Select(node => node.Name));

            var definition = await DatabaseIntegrationTestSupport.WithTimeout(
                Data.Services.TableDesigner.TableDefinitionLoader.LoadAsync(connectionSettings, schemaName, "orders", []),
                IntegrationTimeout,
                "SQL Server table definition");
            Assert.Equal("other_id", Assert.Single(definition.PrimaryKey.ColumnNames));

            var ddl = await DatabaseIntegrationTestSupport.WithTimeout(
                new SchemaMetadataService(connectionSettings).GetDdlAsync(otherOrders),
                IntegrationTimeout,
                "SQL Server DDL");
            Assert.Contains($"create table [{schemaName}].[orders]", ddl, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("[other_id]", ddl, StringComparison.Ordinal);
        }
        finally
        {
            await DatabaseIntegrationTestSupport.ExecuteNonQueryAsync(connectionSettings, $"drop table if exists {schemaName}.orders");
            await DatabaseIntegrationTestSupport.ExecuteNonQueryAsync(connectionSettings, $"drop schema {schemaName}");
        }
    }

    private static bool NameMatches(string actualName, string expectedName)
    {
        return string.Equals(actualName, expectedName, StringComparison.OrdinalIgnoreCase) ||
               actualName.EndsWith("." + expectedName, StringComparison.OrdinalIgnoreCase);
    }
}
