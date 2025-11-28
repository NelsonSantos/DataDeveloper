using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;

public class SqlServerSchemaCache
{
    public List<TableMetadata> Tables { get; set; } = new();

    public static SqlServerSchemaCache FromDatabase(IDbConnection connection)
    {
        var sql = @"
            SELECT 
                s.name AS SchemaName,
                o.name AS TableName,
                c.name AS ColumnName
            FROM sys.objects o
            INNER JOIN sys.columns c ON c.object_id = o.object_id
            INNER JOIN sys.schemas s ON s.schema_id = o.schema_id
            WHERE o.type IN ('U', 'V') -- U = user table, V = view
        ";

        var rows = connection.Query<(string SchemaName, string TableName, string ColumnName)>(sql);

        var grouped = rows
            .GroupBy(r => $"{r.SchemaName}.{r.TableName}")
            .Select(g => new TableMetadata
            {
                Name = g.Key, // Ex: dbo.Produtos
                Columns = g.Select(x => x.ColumnName).Distinct().ToList()
            });

        return new SqlServerSchemaCache { Tables = grouped.ToList() };
    }
}