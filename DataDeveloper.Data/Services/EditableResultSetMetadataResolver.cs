using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Services.Metadata;
using DataDeveloper.Data.Services.SqlDialects;

namespace DataDeveloper.Data.Services;

public static class EditableResultSetMetadataResolver
{
    public static async Task<EditableResultSetMetadata> ResolveAsync(
        IConnectionSettings connectionSettings,
        string statement,
        IReadOnlyCollection<string> resultColumns,
        string? tableNameHint = null,
        IReadOnlyCollection<string>? primaryKeyColumnsHint = null)
    {
        var basicAnalysis = ResultSetEditabilityAnalyzer.Analyze(statement);
        var targetTableName = tableNameHint ?? basicAnalysis.TableName;
        if (!basicAnalysis.IsEditable || string.IsNullOrWhiteSpace(targetTableName))
            return new EditableResultSetMetadata(basicAnalysis, []);

        var tableColumns = await LoadColumnsAsync(connectionSettings, targetTableName);
        if (tableColumns.Count == 0 && primaryKeyColumnsHint is not null)
        {
            tableColumns = resultColumns
                .Select(columnName => new ColumnModel
                {
                    Name = columnName,
                    IsPrimaryKey = primaryKeyColumnsHint.Contains(columnName, StringComparer.OrdinalIgnoreCase)
                })
                .ToList();
        }

        var finalAnalysis = ResultSetEditabilityAnalyzer.Analyze(statement, resultColumns, tableColumns);
        if (!finalAnalysis.IsEditable && string.IsNullOrWhiteSpace(finalAnalysis.Reason))
            finalAnalysis = finalAnalysis with { Reason = "Result set is read-only." };

        finalAnalysis = finalAnalysis with { TableName = targetTableName };
        return new EditableResultSetMetadata(finalAnalysis, tableColumns);
    }

    private static Task<IReadOnlyList<ColumnModel>> LoadColumnsAsync(IConnectionSettings connectionSettings, string tableName)
    {
        var table = DbObjectRef.Parse(DbObjectKind.Table, tableName, SqlDialect.For(connectionSettings.DatabaseType));
        return new SchemaMetadataService(connectionSettings).GetColumnsAsync(table);
    }
}
