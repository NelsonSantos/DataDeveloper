using System.Linq;
using DataDeveloper.Data;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Models.TableDesigner;
using DataDeveloper.Data.Services.TableDesigner;
using Xunit;

namespace DataDeveloper.Tests.Integration;

public class TableDesignerIntegrationTests
{
    private static readonly TimeSpan IntegrationTimeout = TimeSpan.FromSeconds(15);

    public TableDesignerIntegrationTests()
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
    public async Task Provider_AppliesGeneratedCreateTableScript_WithPrimaryKeyForeignKeyAndIndex(DatabaseType databaseType)
    {
        if (!DatabaseIntegrationTestSupport.ShouldRunIntegrationTests())
            return;

        var connectionSettings = DatabaseIntegrationTestSupport.CreateConnectionSettings(databaseType);
        await RunCreateTableFlowAsync(connectionSettings, databaseType);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SqLite_AppliesGeneratedCreateTableScript_WithPrimaryKeyForeignKeyAndIndex()
    {
        if (!DatabaseIntegrationTestSupport.ShouldRunIntegrationTests())
            return;

        var connectionSettings = await DatabaseIntegrationTestSupport.CreateSqLiteConnectionAsync();
        await RunCreateTableFlowAsync(connectionSettings, DatabaseType.SqLite);
    }

    [Theory]
    [Trait("Category", "Integration")]
    [MemberData(nameof(ProviderDatabaseTypes))]
    public async Task Provider_LoadsEditsAndReappliesGeneratedTable(DatabaseType databaseType)
    {
        if (!DatabaseIntegrationTestSupport.ShouldRunIntegrationTests())
            return;

        var connectionSettings = DatabaseIntegrationTestSupport.CreateConnectionSettings(databaseType);
        await RunLoadEditReapplyFlowAsync(connectionSettings, databaseType);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SqLite_LoadsEditsAndReappliesGeneratedTable()
    {
        if (!DatabaseIntegrationTestSupport.ShouldRunIntegrationTests())
            return;

        var connectionSettings = await DatabaseIntegrationTestSupport.CreateSqLiteConnectionAsync();
        await RunLoadEditReapplyFlowAsync(connectionSettings, DatabaseType.SqLite);
    }

    [Theory]
    [Trait("Category", "Integration")]
    [MemberData(nameof(ProviderDatabaseTypes))]
    public async Task Provider_RenamesColumnAndTable_AndChangesColumnType(DatabaseType databaseType)
    {
        if (!DatabaseIntegrationTestSupport.ShouldRunIntegrationTests())
            return;

        var connectionSettings = DatabaseIntegrationTestSupport.CreateConnectionSettings(databaseType);
        await RunRenameAndRetypeFlowAsync(connectionSettings, databaseType);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SqLite_RebuildsTable_PreservingExistingData()
    {
        if (!DatabaseIntegrationTestSupport.ShouldRunIntegrationTests())
            return;

        var connectionSettings = await DatabaseIntegrationTestSupport.CreateSqLiteConnectionAsync();
        var uniqueToken = Guid.NewGuid().ToString("N")[..8];
        var tableName = $"td_{uniqueToken}";
        var table = BuildTableDefinition(DatabaseType.SqLite, tableName);

        foreach (var statement in connectionSettings.GetSqlAnalyzer().SplitStatements(TableDdlScriptBuilder.BuildCreateTableScript(DatabaseType.SqLite, table)))
            await DatabaseIntegrationTestSupport.ExecuteNonQueryAsync(connectionSettings, statement);

        try
        {
            await DatabaseIntegrationTestSupport.ExecuteNonQueryAsync(connectionSettings, $"insert into {tableName} (customer_id) values (1)");

            var loadedColumns = await LoadColumnsAsync(connectionSettings, tableName);
            var originalDefinition = await TableDefinitionLoader.LoadAsync(connectionSettings, string.Empty, tableName, loadedColumns);

            // Add a column and drop the index: SQLite has no ALTER for dropping an index'
            // presence in a single lightweight statement in this builder's model (index
            // structure is only ever recreated wholesale in the rebuild path), so this
            // combination forces the full rebuild path, and the existing row must survive the
            // create-temp/copy/drop/rename cycle. (A renamed FK-bearing column is not exercised
            // here: this pass does not propagate a column rename into FK/index column-name
            // references, so renaming customer_id would produce an inconsistent script — a
            // known follow-up, not attempted in this test.)
            var currentDefinition = CloneDefinition(originalDefinition);
            currentDefinition.Columns.Add(new TableColumnDefinition { Name = "label", DataType = "text", IsNullable = true });
            currentDefinition.Indexes.Clear();

            var rebuildSql = TableDdlScriptBuilder.BuildAlterTableScript(DatabaseType.SqLite, originalDefinition, currentDefinition);
            Assert.Contains("pragma foreign_keys=off;", rebuildSql);

            foreach (var statement in connectionSettings.GetSqlAnalyzer().SplitStatements(rebuildSql))
                await DatabaseIntegrationTestSupport.ExecuteNonQueryAsync(connectionSettings, statement);

            var rowCount = await DatabaseIntegrationTestSupport.ExecuteScalarIntAsync(connectionSettings, $"select count(*) as value from {tableName} where customer_id = 1");
            Assert.Equal(1, rowCount);
        }
        finally
        {
            await DatabaseIntegrationTestSupport.ExecuteNonQueryAsync(connectionSettings, $"drop table {tableName}");
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Postgres_LoadsVarcharColumn_NormalizedFromCharacterVarying_AndReapplyIsNoOp()
    {
        if (!DatabaseIntegrationTestSupport.ShouldRunIntegrationTests())
            return;

        var connectionSettings = DatabaseIntegrationTestSupport.CreateConnectionSettings(DatabaseType.PostgresSql);
        var uniqueToken = Guid.NewGuid().ToString("N")[..8];
        var tableName = $"td_{uniqueToken}";
        var table = BuildTableDefinition(DatabaseType.PostgresSql, tableName);
        table.Columns.Add(new TableColumnDefinition { Name = "sku", DataType = "varchar", Length = 255, IsNullable = true });

        var createSql = TableDdlScriptBuilder.BuildCreateTableScript(DatabaseType.PostgresSql, table);
        foreach (var statement in connectionSettings.GetSqlAnalyzer().SplitStatements(createSql))
            await DatabaseIntegrationTestSupport.ExecuteNonQueryAsync(connectionSettings, statement);

        try
        {
            var loadedColumns = await LoadColumnsAsync(connectionSettings, tableName);
            var originalDefinition = await TableDefinitionLoader.LoadAsync(connectionSettings, string.Empty, tableName, loadedColumns);

            var skuColumn = originalDefinition.Columns.Single(column => string.Equals(column.Name, "sku", StringComparison.OrdinalIgnoreCase));
            Assert.Equal("varchar", skuColumn.DataType, StringComparer.OrdinalIgnoreCase);

            var unmodifiedAlterSql = TableDdlScriptBuilder.BuildAlterTableScript(DatabaseType.PostgresSql, originalDefinition, CloneDefinition(originalDefinition));
            Assert.Equal(string.Empty, unmodifiedAlterSql);
        }
        finally
        {
            await DatabaseIntegrationTestSupport.ExecuteNonQueryAsync(connectionSettings, $"drop table {tableName}");
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SqLite_LoadsUntouchedTable_ReapplyIsNoOp()
    {
        if (!DatabaseIntegrationTestSupport.ShouldRunIntegrationTests())
            return;

        var connectionSettings = await DatabaseIntegrationTestSupport.CreateSqLiteConnectionAsync();
        var uniqueToken = Guid.NewGuid().ToString("N")[..8];
        var tableName = $"td_{uniqueToken}";
        var table = BuildTableDefinition(DatabaseType.SqLite, tableName);

        foreach (var statement in connectionSettings.GetSqlAnalyzer().SplitStatements(TableDdlScriptBuilder.BuildCreateTableScript(DatabaseType.SqLite, table)))
            await DatabaseIntegrationTestSupport.ExecuteNonQueryAsync(connectionSettings, statement);

        try
        {
            var loadedColumns = await LoadColumnsAsync(connectionSettings, tableName);
            var originalDefinition = await TableDefinitionLoader.LoadAsync(connectionSettings, string.Empty, tableName, loadedColumns);

            var idColumn = originalDefinition.Columns.Single(column => string.Equals(column.Name, "id", StringComparison.OrdinalIgnoreCase));
            Assert.False(idColumn.IsNullable);

            var unmodifiedAlterSql = TableDdlScriptBuilder.BuildAlterTableScript(DatabaseType.SqLite, originalDefinition, CloneDefinition(originalDefinition));
            Assert.Equal(string.Empty, unmodifiedAlterSql);
        }
        finally
        {
            await DatabaseIntegrationTestSupport.ExecuteNonQueryAsync(connectionSettings, $"drop table {tableName}");
        }
    }

    private static async Task RunLoadEditReapplyFlowAsync(IConnectionSettings connectionSettings, DatabaseType databaseType)
    {
        var uniqueToken = Guid.NewGuid().ToString("N")[..8];
        var tableName = $"td_{uniqueToken}";
        var table = BuildTableDefinition(databaseType, tableName);

        var createSql = TableDdlScriptBuilder.BuildCreateTableScript(databaseType, table);
        foreach (var statement in connectionSettings.GetSqlAnalyzer().SplitStatements(createSql))
        {
            await DatabaseIntegrationTestSupport.WithTimeout(
                DatabaseIntegrationTestSupport.ExecuteNonQueryAsync(connectionSettings, statement),
                IntegrationTimeout,
                $"{databaseType} create table statement");
        }

        try
        {
            var loadedColumns = await LoadColumnsAsync(connectionSettings, tableName);
            var loadTimeout = databaseType == DatabaseType.Oracle ? TimeSpan.FromSeconds(30) : IntegrationTimeout;
            var originalDefinition = await DatabaseIntegrationTestSupport.WithTimeout(
                TableDefinitionLoader.LoadAsync(connectionSettings, string.Empty, tableName, loadedColumns),
                loadTimeout,
                $"{databaseType} load table definition");

            Assert.Equal("id", Assert.Single(originalDefinition.PrimaryKey.ColumnNames), StringComparer.OrdinalIgnoreCase);
            var loadedForeignKey = Assert.Single(originalDefinition.ForeignKeys);
            Assert.Equal("customer_id", Assert.Single(loadedForeignKey.ColumnNames), StringComparer.OrdinalIgnoreCase);
            Assert.Equal("customer_id", Assert.Single(loadedForeignKey.ReferencedColumnNames), StringComparer.OrdinalIgnoreCase);
            var loadedIndex = Assert.Single(originalDefinition.Indexes);
            Assert.Equal("customer_id", Assert.Single(loadedIndex.Columns).Name, StringComparer.OrdinalIgnoreCase);

            // Regression check: diffing the freshly-loaded definition against a clone of itself
            // (i.e. opening Edit table and changing nothing) must produce an empty script. This
            // guards against providers that report a non-zero internal storage size as "Length"
            // even for types with no length concept (e.g. SQL Server's max_length for decimal
            // columns), which previously made every untouched column look retyped.
            var unmodifiedAlterSql = TableDdlScriptBuilder.BuildAlterTableScript(databaseType, originalDefinition, CloneDefinition(originalDefinition));
            Assert.Equal(string.Empty, unmodifiedAlterSql);

            // Only add a column here: dropping the FK-backing index (MySQL) or altering the PK
            // in the same script as other diffs is exercised by the unit tests already; this
            // integration test's goal is to prove the full load -> alter -> apply pipeline works
            // end-to-end against a real database, not to combine every diff type at once.
            // Clone from originalDefinition (not a fresh BuildTableDefinition) so existing
            // columns carry exactly the same Length/Precision/Scale/OriginalName the loader
            // read from the live database -- a freshly-declared column can have a slightly
            // different structured representation of the same type (e.g. Oracle's implicit
            // NUMBER precision) than what user_tab_columns reports, which would look like a
            // spurious diff and produce a no-op ALTER that some providers (Oracle) reject.
            var currentDefinition = CloneDefinition(originalDefinition);
            currentDefinition.Columns.Add(new TableColumnDefinition
            {
                Name = "note",
                DataType = BuildNoteColumnDataType(databaseType),
                Length = databaseType == DatabaseType.SqLite ? null : 100,
                IsNullable = true
            });

            var alterSql = TableDdlScriptBuilder.BuildAlterTableScript(databaseType, originalDefinition, currentDefinition);
            Assert.NotEmpty(alterSql);

            foreach (var statement in connectionSettings.GetSqlAnalyzer().SplitStatements(alterSql))
            {
                await DatabaseIntegrationTestSupport.WithTimeout(
                    DatabaseIntegrationTestSupport.ExecuteNonQueryAsync(connectionSettings, statement),
                    IntegrationTimeout,
                    $"{databaseType} alter table statement");
            }

            var insertSql = $"insert into {tableName} (customer_id, note) values (1, {(databaseType == DatabaseType.SqlServer ? "N'ok'" : "'ok'")})";
            await DatabaseIntegrationTestSupport.WithTimeout(
                DatabaseIntegrationTestSupport.ExecuteNonQueryAsync(connectionSettings, insertSql),
                IntegrationTimeout,
                $"{databaseType} insert into altered table");

            var rowCount = await DatabaseIntegrationTestSupport.WithTimeout(
                DatabaseIntegrationTestSupport.ExecuteScalarIntAsync(connectionSettings, $"select count(*) as value from {tableName}"),
                IntegrationTimeout,
                $"{databaseType} count rows in altered table");

            Assert.Equal(1, rowCount);
        }
        finally
        {
            await DatabaseIntegrationTestSupport.ExecuteNonQueryAsync(connectionSettings, $"drop table {tableName}");
        }
    }

    private static TableDefinition CloneDefinition(TableDefinition source)
    {
        var clone = new TableDefinition
        {
            SchemaName = source.SchemaName,
            TableName = source.TableName
        };

        foreach (var column in source.Columns)
        {
            clone.Columns.Add(new TableColumnDefinition
            {
                OriginalName = column.OriginalName,
                Name = column.Name,
                DataType = column.DataType,
                Length = column.Length,
                Precision = column.Precision,
                Scale = column.Scale,
                IsNullable = column.IsNullable,
                IsIdentity = column.IsIdentity,
                DefaultValue = column.DefaultValue
            });
        }

        clone.PrimaryKey.Name = source.PrimaryKey.Name;
        clone.PrimaryKey.ColumnNames.AddRange(source.PrimaryKey.ColumnNames);

        foreach (var foreignKey in source.ForeignKeys)
        {
            var clonedForeignKey = new TableForeignKeyDefinition
            {
                Name = foreignKey.Name,
                ReferencedSchemaName = foreignKey.ReferencedSchemaName,
                ReferencedTableName = foreignKey.ReferencedTableName,
                OnDeleteAction = foreignKey.OnDeleteAction,
                OnUpdateAction = foreignKey.OnUpdateAction
            };
            clonedForeignKey.ColumnNames.AddRange(foreignKey.ColumnNames);
            clonedForeignKey.ReferencedColumnNames.AddRange(foreignKey.ReferencedColumnNames);
            clone.ForeignKeys.Add(clonedForeignKey);
        }

        foreach (var index in source.Indexes)
        {
            var clonedIndex = new TableIndexDefinition { Name = index.Name, IsUnique = index.IsUnique };
            clonedIndex.Columns.AddRange(index.Columns.Select(column => new TableIndexColumnDefinition
            {
                Name = column.Name,
                Descending = column.Descending
            }));
            clone.Indexes.Add(clonedIndex);
        }

        return clone;
    }

    private static async Task RunRenameAndRetypeFlowAsync(IConnectionSettings connectionSettings, DatabaseType databaseType)
    {
        var uniqueToken = Guid.NewGuid().ToString("N")[..8];
        var tableName = $"td_{uniqueToken}";
        var table = BuildTableDefinition(databaseType, tableName);

        foreach (var statement in connectionSettings.GetSqlAnalyzer().SplitStatements(TableDdlScriptBuilder.BuildCreateTableScript(databaseType, table)))
        {
            await DatabaseIntegrationTestSupport.WithTimeout(
                DatabaseIntegrationTestSupport.ExecuteNonQueryAsync(connectionSettings, statement), IntegrationTimeout, $"{databaseType} create table statement");
        }

        var finalTableName = $"{tableName}_r";

        try
        {
            // Step 1: add a plain "label" column, so step 2 can rename+retype an existing
            // column without touching the FK/index-bearing customer_id column.
            var loadedColumns = await LoadColumnsAsync(connectionSettings, tableName);
            var originalDefinition = await TableDefinitionLoader.LoadAsync(connectionSettings, string.Empty, tableName, loadedColumns);
            var withLabelColumn = CloneDefinition(originalDefinition);
            withLabelColumn.Columns.Add(new TableColumnDefinition { Name = "label", DataType = BuildNoteColumnDataType(databaseType), Length = 50, IsNullable = true });

            foreach (var statement in connectionSettings.GetSqlAnalyzer().SplitStatements(
                TableDdlScriptBuilder.BuildAlterTableScript(databaseType, originalDefinition, withLabelColumn)))
            {
                await DatabaseIntegrationTestSupport.WithTimeout(
                    DatabaseIntegrationTestSupport.ExecuteNonQueryAsync(connectionSettings, statement), IntegrationTimeout, $"{databaseType} add label column");
            }

            // Step 2: rename "label" -> "note" and widen it, and rename the table.
            var reloadedColumns = await LoadColumnsAsync(connectionSettings, tableName);
            var reloadedDefinition = await TableDefinitionLoader.LoadAsync(connectionSettings, string.Empty, tableName, reloadedColumns);
            var renamedDefinition = CloneDefinition(reloadedDefinition);
            var labelColumn = renamedDefinition.Columns.Single(column => string.Equals(column.Name, "label", StringComparison.OrdinalIgnoreCase));
            labelColumn.Name = "note";
            labelColumn.Length = 200;
            renamedDefinition.TableName = finalTableName;

            var renameSql = TableDdlScriptBuilder.BuildAlterTableScript(databaseType, reloadedDefinition, renamedDefinition);
            foreach (var statement in connectionSettings.GetSqlAnalyzer().SplitStatements(renameSql))
            {
                await DatabaseIntegrationTestSupport.WithTimeout(
                    DatabaseIntegrationTestSupport.ExecuteNonQueryAsync(connectionSettings, statement), IntegrationTimeout, $"{databaseType} rename column and table");
            }

            var insertSql = $"insert into {finalTableName} (customer_id, note) values (1, {(databaseType == DatabaseType.SqlServer ? "N'ok'" : "'ok'")})";
            await DatabaseIntegrationTestSupport.WithTimeout(
                DatabaseIntegrationTestSupport.ExecuteNonQueryAsync(connectionSettings, insertSql), IntegrationTimeout, $"{databaseType} insert after rename");

            var rowCount = await DatabaseIntegrationTestSupport.WithTimeout(
                DatabaseIntegrationTestSupport.ExecuteScalarIntAsync(connectionSettings, $"select count(*) as value from {finalTableName}"),
                IntegrationTimeout, $"{databaseType} count rows after rename");

            Assert.Equal(1, rowCount);
        }
        finally
        {
            await DatabaseIntegrationTestSupport.ExecuteNonQueryAsync(connectionSettings, $"drop table {finalTableName}");
        }
    }

    private static string BuildNoteColumnDataType(DatabaseType databaseType)
    {
        return databaseType switch
        {
            DatabaseType.SqlServer => "nvarchar",
            DatabaseType.MySql => "varchar",
            DatabaseType.PostgresSql => "varchar",
            DatabaseType.Oracle => "varchar2",
            _ => "text"
        };
    }

    private static async Task<IReadOnlyList<ColumnModel>> LoadColumnsAsync(IConnectionSettings connectionSettings, string tableName)
    {
        var schemaExplorer = connectionSettings.GetSchemaExplorer();
        await schemaExplorer.InitializeSchemaNode();
        var root = schemaExplorer.RootConnections.Single();
        var tablesFolder = root.Children.Single(node => node.NodeType == NodeType.Tables);
        await schemaExplorer.LoadNodeAsync(tablesFolder);
        var tableNode = tablesFolder.Children.Single(node =>
            string.Equals(node.Name, tableName, StringComparison.OrdinalIgnoreCase) ||
            node.Name.EndsWith("." + tableName, StringComparison.OrdinalIgnoreCase));
        var columnsFolder = tableNode.Children.Single(node => node.NodeType == NodeType.Columns);
        await schemaExplorer.LoadNodeAsync(columnsFolder);

        return columnsFolder.Children
            .Where(node => node.NodeType == NodeType.Column && node.Tag is ColumnModel)
            .Select(node => (ColumnModel)node.Tag!)
            .ToList();
    }

    private static async Task RunCreateTableFlowAsync(IConnectionSettings connectionSettings, DatabaseType databaseType)
    {
        var uniqueToken = Guid.NewGuid().ToString("N")[..8];
        var tableName = $"td_{uniqueToken}";
        var table = BuildTableDefinition(databaseType, tableName);

        var generatedSql = TableDdlScriptBuilder.BuildCreateTableScript(databaseType, table);
        var statements = connectionSettings.GetSqlAnalyzer().SplitStatements(generatedSql);
        Assert.NotEmpty(statements);

        try
        {
            foreach (var statement in statements)
            {
                await DatabaseIntegrationTestSupport.WithTimeout(
                    DatabaseIntegrationTestSupport.ExecuteNonQueryAsync(connectionSettings, statement),
                    IntegrationTimeout,
                    $"{databaseType} create table statement");
            }

            var insertSql = $"insert into {table.TableName} (customer_id) values (1)";
            await DatabaseIntegrationTestSupport.WithTimeout(
                DatabaseIntegrationTestSupport.ExecuteNonQueryAsync(connectionSettings, insertSql),
                IntegrationTimeout,
                $"{databaseType} insert into generated table");

            var rowCount = await DatabaseIntegrationTestSupport.WithTimeout(
                DatabaseIntegrationTestSupport.ExecuteScalarIntAsync(connectionSettings, $"select count(*) as value from {table.TableName}"),
                IntegrationTimeout,
                $"{databaseType} count rows in generated table");

            Assert.Equal(1, rowCount);
        }
        finally
        {
            await DatabaseIntegrationTestSupport.ExecuteNonQueryAsync(connectionSettings, $"drop table {table.TableName}");
        }
    }

    private static TableDefinition BuildTableDefinition(DatabaseType databaseType, string tableName)
    {
        var defaultType = ProviderDataTypeCatalog.GetDefaultDataType(databaseType);
        var table = new TableDefinition
        {
            TableName = tableName
        };

        table.Columns.Add(new TableColumnDefinition
        {
            Name = "id",
            DataType = defaultType.Name,
            IsNullable = false,
            IsIdentity = defaultType.SupportsIdentity
        });

        table.Columns.Add(new TableColumnDefinition
        {
            Name = "customer_id",
            DataType = defaultType.Name,
            IsNullable = false
        });

        table.PrimaryKey.Name = $"pk_{tableName}";
        table.PrimaryKey.ColumnNames.Add("id");

        table.ForeignKeys.Add(new TableForeignKeyDefinition
        {
            Name = $"fk_{tableName}_customers",
            ReferencedTableName = "customers",
            OnDeleteAction = "cascade"
        });
        table.ForeignKeys[0].ColumnNames.Add("customer_id");
        table.ForeignKeys[0].ReferencedColumnNames.Add("customer_id");

        table.Indexes.Add(new TableIndexDefinition
        {
            Name = $"ix_{tableName}_customer_id"
        });
        table.Indexes[0].Columns.Add(new TableIndexColumnDefinition { Name = "customer_id" });

        return table;
    }
}
