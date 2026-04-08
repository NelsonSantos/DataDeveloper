using Dapper;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Models;

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

    private static async Task<IReadOnlyList<ColumnModel>> LoadColumnsAsync(IConnectionSettings connectionSettings, string tableName)
    {
        var provider = connectionSettings.GetDatabaseProvider();
        await using var connection = provider.GetConnection();
        var lookupName = NormalizeLookupTableName(connectionSettings.DatabaseType, tableName);
        var columnStatement = provider.GetColumnStatement();
        object? parameters = new { TableName = lookupName };

        if (connectionSettings.DatabaseType == DatabaseType.SqLite && !string.IsNullOrWhiteSpace(lookupName))
        {
            var escapedTableName = lookupName.Replace("'", "''", StringComparison.Ordinal);
            columnStatement = columnStatement.Replace("__table_name__", $"'{escapedTableName}'", StringComparison.Ordinal);
            parameters = null;
        }

        var columns = await connection.QueryAsync<ColumnModel>(columnStatement, parameters);
        return columns.ToList();
    }

    private static string NormalizeLookupTableName(DatabaseType databaseType, string tableName)
    {
        var identifiers = SplitIdentifiers(tableName);
        if (identifiers.Count == 0)
            return tableName;

        return databaseType switch
        {
            DatabaseType.SqlServer => string.Join(".", identifiers),
            DatabaseType.MySql or DatabaseType.PostgresSql or DatabaseType.Oracle or DatabaseType.SqLite => identifiers[^1],
            _ => tableName
        };
    }

    private static List<string> SplitIdentifiers(string value)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        char? quote = null;

        for (var index = 0; index < value.Length; index++)
        {
            var ch = value[index];
            if (quote is null)
            {
                if (ch == '.')
                {
                    FlushCurrent();
                    continue;
                }

                if (ch is '[' or '"' or '`')
                {
                    quote = ch;
                    continue;
                }

                current.Append(ch);
                continue;
            }

            var isClosing = quote switch
            {
                '[' => ch == ']',
                '"' => ch == '"',
                '`' => ch == '`',
                _ => false
            };

            if (isClosing)
            {
                quote = null;
                continue;
            }

            current.Append(ch);
        }

        FlushCurrent();
        return result;

        void FlushCurrent()
        {
            var part = current.ToString().Trim();
            current.Clear();
            if (!string.IsNullOrWhiteSpace(part))
                result.Add(part);
        }
    }
}
