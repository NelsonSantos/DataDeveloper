using System.Data.Common;
using DataDeveloper.Data;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Models.TableDesigner;
using DataDeveloper.Data.Services;
using DataDeveloper.Data.Services.TableDesigner;
using Xunit;

namespace DataDeveloper.Tests.Integration;

/// <summary>
/// Tables whose names only exist in a specific case: quoted mixed-case names on Oracle (which
/// upper-cases unquoted names) and unquoted names typed in another case on PostgreSQL (which
/// lower-cases them).
/// </summary>
[Collection(DatabaseIntegrationCollection.Name)]
public class IdentifierCaseIntegrationTests
{
    private static readonly TimeSpan IntegrationTimeout = TimeSpan.FromSeconds(30);

    public IdentifierCaseIntegrationTests()
    {
        DatabaseIntegrationTestSupport.EnsureDatabaseServices();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Oracle_QuotedMixedCaseTable_WorksInTreeGridAndEditTable()
    {
        if (!DatabaseIntegrationTestSupport.ShouldRunIntegrationTests())
            return;

        var settings = DatabaseIntegrationTestSupport.CreateConnectionSettings(DatabaseType.Oracle);
        var tableName = $"Mixed_{Guid.NewGuid().ToString("N")[..6]}";
        await DatabaseIntegrationTestSupport.ExecuteNonQueryAsync(settings,
            $"create table \"{tableName}\" (\"Id\" number constraint \"Pk_{tableName}\" primary key, \"Nome\" varchar2(20))");
        try
        {
            await DatabaseIntegrationTestSupport.ExecuteNonQueryAsync(settings, $"insert into \"{tableName}\" values (1, 'a')");

            // Tree: columns and keys of the mixed-case table load.
            var explorer = settings.GetSchemaExplorer();
            await DatabaseIntegrationTestSupport.WithTimeout(explorer.InitializeSchemaNode(), IntegrationTimeout, "Oracle schema initialization");
            var tableNode = explorer.RootConnections[0].Children.Single(node => node.NodeType == NodeType.Tables)
                .Children.Single(node => node.Name == tableName);
            var columns = await LoadFolderAsync(explorer, tableNode, NodeType.Columns);
            Assert.Equal(["Id", "Nome"], columns.Select(node => node.Name));
            Assert.Equal($"Pk_{tableName}", Assert.Single(await LoadFolderAsync(explorer, tableNode, NodeType.Keys)).Name);

            // Grid: the quoted table in the query is editable and the update hits it.
            var select = $"select * from \"{tableName}\"";
            var (hint, resultColumns) = await ReadResultSchemaAsync(settings, select);
            var metadata = await EditableResultSetMetadataResolver.ResolveAsync(settings, select, resultColumns, hint, []);
            Assert.True(metadata.IsEditable, metadata.Reason);
            var update = EditableResultSetCommandBuilder.BuildUpdate(DatabaseType.Oracle, metadata.TableName!, resultColumns, metadata.TableColumns, [1, "a"], [1, "b"]);
            Assert.Equal($"update \"{tableName}\" set \"Nome\" = :p0 where \"Id\" = :p1;", update!.Sql);
            Assert.Equal(1, await DatabaseIntegrationTestSupport.ExecuteNonQueryAsync(settings, update.Sql, update.Parameters));

            // Edit Table: loading from the tree node and adding a column alters this table.
            var definition = await TableDefinitionLoader.LoadAsync(
                settings, string.Empty, tableNode.Name, columns.Select(node => (ColumnModel)node.Tag!).ToList());
            var edited = CloneWithNewColumn(definition, new TableColumnDefinition { Name = "obs", DataType = "varchar2", Length = 30, IsNullable = true });
            var alter = TableDdlScriptBuilder.BuildAlterTableScript(DatabaseType.Oracle, definition, edited);
            Assert.Contains($"\"{tableName}\"", alter, StringComparison.Ordinal);
            foreach (var statement in settings.GetSqlAnalyzer().SplitStatements(alter))
                await DatabaseIntegrationTestSupport.ExecuteNonQueryAsync(settings, statement);

            await explorer.RefreshSchemaObjectAsync($"alter table \"{tableName}\" add obs varchar2(30)");
            Assert.Equal(["Id", "Nome", "OBS"], tableNode.Children.Single(node => node.NodeType == NodeType.Columns).Children.Select(node => node.Name));
        }
        finally
        {
            await DatabaseIntegrationTestSupport.ExecuteNonQueryAsync(settings, $"drop table \"{tableName}\"");
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Postgres_UnquotedNameTypedInMixedCase_IsEditableInTheGrid()
    {
        if (!DatabaseIntegrationTestSupport.ShouldRunIntegrationTests())
            return;

        var settings = DatabaseIntegrationTestSupport.CreateConnectionSettings(DatabaseType.PostgresSql);
        var tableName = $"lower_{Guid.NewGuid().ToString("N")[..6]}";
        await DatabaseIntegrationTestSupport.ExecuteNonQueryAsync(settings, $"create table {tableName} (id int primary key, nome text)");
        try
        {
            await DatabaseIntegrationTestSupport.ExecuteNonQueryAsync(settings, $"insert into {tableName} values (1, 'a')");

            // PostgreSQL lower-cases the unquoted name, so this is the same table.
            var select = $"select * from {tableName.ToUpperInvariant()}";
            var (hint, resultColumns) = await ReadResultSchemaAsync(settings, select);
            var metadata = await EditableResultSetMetadataResolver.ResolveAsync(settings, select, resultColumns, hint, []);

            Assert.True(metadata.IsEditable, metadata.Reason);
            var update = EditableResultSetCommandBuilder.BuildUpdate(DatabaseType.PostgresSql, metadata.TableName!, resultColumns, metadata.TableColumns, [1, "a"], [1, "b"]);
            Assert.Equal($"update \"{tableName}\" set \"nome\" = @p0 where \"id\" = @p1;", update!.Sql);
            Assert.Equal(1, await DatabaseIntegrationTestSupport.ExecuteNonQueryAsync(settings, update.Sql, update.Parameters));
        }
        finally
        {
            await DatabaseIntegrationTestSupport.ExecuteNonQueryAsync(settings, $"drop table {tableName}");
        }
    }

    // Mirrors TabDataGridViewModel: the driver's base table name (if any) and the result columns.
    private static async Task<(string? TableNameHint, List<string> Columns)> ReadResultSchemaAsync(IConnectionSettings settings, string select)
    {
        await using var connection = settings.GetDatabaseProvider().GetConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = select;
        await using var reader = await command.ExecuteReaderAsync();
        var schema = reader.GetColumnSchema();
        var baseTable = schema.FirstOrDefault(column => !string.IsNullOrWhiteSpace(column.BaseTableName))?.BaseTableName;
        return (baseTable, schema.Select(column => column.ColumnName).ToList());
    }

    private static async Task<IReadOnlyList<SchemaNode>> LoadFolderAsync(ISchemaExplorer explorer, SchemaNode table, NodeType folderType)
    {
        var folder = table.Children.Single(node => node.NodeType == folderType);
        await DatabaseIntegrationTestSupport.WithTimeout(explorer.LoadNodeAsync(folder), IntegrationTimeout, $"{folderType} load");
        return folder.Children.ToList();
    }

    private static TableDefinition CloneWithNewColumn(TableDefinition definition, TableColumnDefinition newColumn)
    {
        var clone = new TableDefinition { SchemaName = definition.SchemaName, TableName = definition.TableName };
        foreach (var column in definition.Columns)
        {
            clone.Columns.Add(new TableColumnDefinition
            {
                OriginalName = column.OriginalName, Name = column.Name, DataType = column.DataType, Length = column.Length,
                Precision = column.Precision, Scale = column.Scale, IsNullable = column.IsNullable, IsIdentity = column.IsIdentity,
                DefaultValue = column.DefaultValue
            });
        }

        clone.Columns.Add(newColumn);
        clone.PrimaryKey.Name = definition.PrimaryKey.Name;
        clone.PrimaryKey.ColumnNames.AddRange(definition.PrimaryKey.ColumnNames);
        clone.ForeignKeys.AddRange(definition.ForeignKeys);
        clone.Indexes.AddRange(definition.Indexes);
        return clone;
    }
}
