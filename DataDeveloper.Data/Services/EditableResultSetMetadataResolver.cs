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

        // The driver's hint is the stored name; a name read from the query text is resolved the
        // way the database would (e.g. Oracle upper-cases unquoted names) and kept quoted from
        // here on, so the generated DML refers to exactly that table.
        var dialect = SqlDialect.For(connectionSettings.DatabaseType);
        var tableNameParts = tableNameHint is null
            ? dialect.ResolveQualifiedName(targetTableName)
            : dialect.SplitQualifiedName(targetTableName);
        if (tableNameHint is null)
            targetTableName = string.Join(".", tableNameParts.Select(dialect.QuoteIdentifier));

        var tableColumns = await LoadColumnsAsync(connectionSettings, tableNameParts);
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

    private static Task<IReadOnlyList<ColumnModel>> LoadColumnsAsync(IConnectionSettings connectionSettings, IReadOnlyList<string> tableNameParts)
    {
        if (tableNameParts.Count == 0)
            return Task.FromResult<IReadOnlyList<ColumnModel>>([]);

        var table = new DbObjectRef(
            DbObjectKind.Table,
            tableNameParts.Count > 1 ? tableNameParts[^2] : null,
            tableNameParts[^1]);
        return new SchemaMetadataService(connectionSettings).GetColumnsAsync(table);
    }
}
