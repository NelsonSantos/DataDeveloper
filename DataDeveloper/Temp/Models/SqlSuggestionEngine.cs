using System;
using System.Collections.Generic;
using System.Linq;
using AvaloniaEdit.CodeCompletion;

public class SqlSuggestionEngine
{
    private readonly SqlServerSchemaCache _schema;

    public SqlSuggestionEngine(SqlServerSchemaCache schema)
    {
        _schema = schema;
    }

    public IEnumerable<ICompletionData> GetSuggestions(SqlContextResult context)
    {
        var suggestions = new List<ICompletionData>();

        // if (context.AfterTableAlias && context.CurrentAlias != null)
        // {
        //     if (context.Aliases.TryGetValue(context.CurrentAlias, out var tableName))
        //     {
        //         var table = _schema.Tables.FirstOrDefault(t =>
        //             t.Name.EndsWith(tableName, StringComparison.OrdinalIgnoreCase));
        //         if (table != null)
        //             return table.Columns;
        //     }
        //
        //     return Enumerable.Empty<string>();
        // }

        if (context.InFromClause)
        {
            suggestions.AddRange(_schema.Tables.Select(t => new SqlCompletionData(t.Name, $"[table]")));
        }
        
        // if (context.InSelectList)
        //     return _schema.Tables.SelectMany(t => t.Columns).Distinct();

        if (context.BetweenSelectAndFrom)
        {
            foreach (var table in _schema.Tables)
            {
                foreach (var column in table.Columns)
                {
                    suggestions.Add(new SqlCompletionData(column, $"[{table.Name}].[{column}]"));
                }
            }
        }        
        return suggestions;
        //return Enumerable.Empty<ICompletionData>();
    }
}