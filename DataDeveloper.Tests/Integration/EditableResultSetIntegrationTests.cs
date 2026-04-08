using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Services;
using Xunit;

namespace DataDeveloper.Tests.Integration;

public class EditableResultSetIntegrationTests
{
    private static readonly TimeSpan IntegrationTimeout = TimeSpan.FromSeconds(15);

    public EditableResultSetIntegrationTests()
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
    public async Task Provider_EditableResultSetCommands_InsertUpdateDeleteAndRefresh(DatabaseType databaseType)
    {
        if (!DatabaseIntegrationTestSupport.ShouldRunIntegrationTests())
            return;

        var connectionSettings = DatabaseIntegrationTestSupport.CreateConnectionSettings(databaseType);
        await RunEditableResultSetFlowAsync(connectionSettings, databaseType.ToString());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SqLite_EditableResultSetCommands_InsertUpdateDeleteAndRefresh()
    {
        if (!DatabaseIntegrationTestSupport.ShouldRunIntegrationTests())
            return;

        var connectionSettings = await DatabaseIntegrationTestSupport.CreateSqLiteConnectionAsync();
        await RunEditableResultSetFlowAsync(connectionSettings, "SQLite");
    }

    private static async Task RunEditableResultSetFlowAsync(IConnectionSettings connectionSettings, string providerLabel)
    {
        var uniqueToken = Guid.NewGuid().ToString("N")[..10];
        var insertedName = $"Integration {uniqueToken}";
        var updatedName = $"Updated {uniqueToken}";
        var email = $"integration_{uniqueToken}@example.com";
        var emailLiteral = DatabaseIntegrationTestSupport.QuoteSqlLiteral(email);
        var selectAllSql = "select * from customers order by customer_id";
        var lookupSql = $"select * from customers where email = {emailLiteral}";

        try
        {
            var initialSnapshot = await DatabaseIntegrationTestSupport.WithTimeout(
                DatabaseIntegrationTestSupport.ExecuteQuerySnapshotAsync(connectionSettings, selectAllSql),
                IntegrationTimeout,
                $"{providerLabel} initial customer snapshot");

            var metadata = await DatabaseIntegrationTestSupport.WithTimeout(
                EditableResultSetMetadataResolver.ResolveAsync(
                    connectionSettings,
                    selectAllSql,
                    initialSnapshot.Columns,
                    primaryKeyColumnsHint: initialSnapshot.Schema
                        .Where(column => column.IsKey == true && !string.IsNullOrWhiteSpace(column.ColumnName))
                        .Select(column => column.ColumnName!)
                        .ToList()),
                IntegrationTimeout,
                $"{providerLabel} editable metadata");

            Assert.True(metadata.IsEditable);
            Assert.False(string.IsNullOrWhiteSpace(metadata.TableName));

            var insertValues = CreateRowValues(initialSnapshot.Columns, insertedName, email);
            var insertCommand = EditableResultSetCommandBuilder.BuildInsert(
                connectionSettings.DatabaseType,
                metadata.TableName!,
                initialSnapshot.Columns,
                metadata.TableColumns,
                insertValues);

            Assert.NotNull(insertCommand);
            var insertedRows = await DatabaseIntegrationTestSupport.WithTimeout(
                DatabaseIntegrationTestSupport.ExecuteNonQueryAsync(connectionSettings, insertCommand!.Sql, insertCommand.Parameters),
                IntegrationTimeout,
                $"{providerLabel} insert editable row");
            Assert.True(insertedRows > 0);

            var insertedSnapshot = await DatabaseIntegrationTestSupport.WithTimeout(
                DatabaseIntegrationTestSupport.ExecuteQuerySnapshotAsync(connectionSettings, lookupSql),
                IntegrationTimeout,
                $"{providerLabel} load inserted row");
            var insertedRow = Assert.Single(insertedSnapshot.Rows);

            var updatedValues = insertedRow.ToArray();
            updatedValues[GetColumnIndex(insertedSnapshot.Columns, "name")] = updatedName;
            var updateCommand = EditableResultSetCommandBuilder.BuildUpdate(
                connectionSettings.DatabaseType,
                metadata.TableName,
                insertedSnapshot.Columns,
                metadata.TableColumns,
                insertedRow,
                updatedValues);

            Assert.NotNull(updateCommand);
            var updatedRows = await DatabaseIntegrationTestSupport.WithTimeout(
                DatabaseIntegrationTestSupport.ExecuteNonQueryAsync(connectionSettings, updateCommand!.Sql, updateCommand.Parameters),
                IntegrationTimeout,
                $"{providerLabel} update editable row");
            Assert.True(updatedRows > 0);

            var refreshedAfterUpdate = await DatabaseIntegrationTestSupport.WithTimeout(
                DatabaseIntegrationTestSupport.ExecuteQuerySnapshotAsync(connectionSettings, lookupSql),
                IntegrationTimeout,
                $"{providerLabel} refresh after update");
            var updatedRow = Assert.Single(refreshedAfterUpdate.Rows);
            Assert.Equal(updatedName, Convert.ToString(updatedRow[GetColumnIndex(refreshedAfterUpdate.Columns, "name")]));

            var deleteCommand = EditableResultSetCommandBuilder.BuildDelete(
                connectionSettings.DatabaseType,
                metadata.TableName,
                refreshedAfterUpdate.Columns,
                metadata.TableColumns,
                updatedRow);

            Assert.NotNull(deleteCommand);
            var deletedRows = await DatabaseIntegrationTestSupport.WithTimeout(
                DatabaseIntegrationTestSupport.ExecuteNonQueryAsync(connectionSettings, deleteCommand!.Sql, deleteCommand.Parameters),
                IntegrationTimeout,
                $"{providerLabel} delete editable row");
            Assert.True(deletedRows > 0);

            var refreshedAfterDelete = await DatabaseIntegrationTestSupport.WithTimeout(
                DatabaseIntegrationTestSupport.ExecuteQuerySnapshotAsync(connectionSettings, lookupSql),
                IntegrationTimeout,
                $"{providerLabel} refresh after delete");
            Assert.Empty(refreshedAfterDelete.Rows);
        }
        finally
        {
            await DatabaseIntegrationTestSupport.ExecuteNonQueryAsync(
                connectionSettings,
                $"delete from customers where email = {emailLiteral}");
        }
    }

    private static object?[] CreateRowValues(IReadOnlyList<string> columns, string name, string email)
    {
        var values = new object?[columns.Count];
        values[GetColumnIndex(columns, "name")] = name;
        values[GetColumnIndex(columns, "email")] = email;
        return values;
    }

    private static int GetColumnIndex(IReadOnlyList<string> columns, string name)
    {
        for (var index = 0; index < columns.Count; index++)
        {
            if (string.Equals(columns[index], name, StringComparison.OrdinalIgnoreCase))
                return index;
        }

        throw new InvalidOperationException($"Column '{name}' was not found in the result set.");
    }
}
