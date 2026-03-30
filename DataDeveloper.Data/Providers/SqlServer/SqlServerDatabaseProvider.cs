using System.Data;
using System.Data.Common;
using System.Text;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Services;
using Microsoft.Data.SqlClient;

namespace DataDeveloper.Data.Providers.SqlServer;

public class SqlServerDatabaseProvider : DatabaseProviderBase<SqlServerConnectionSettings>
{
    public SqlServerDatabaseProvider(SqlServerConnectionSettings connectionSettings) 
        : base(connectionSettings)
    {
    }

    public override DbConnection GetConnection()
    {
        var connectionString =
            $"Server={ConnectionSettings.Server};" +
            $"Database={ConnectionSettings.Database};" +
            $"User Id={ConnectionSettings.User};" +
            $"Password={ConnectionSettings.Password};" +
            $"Encrypt={ConnectionSettings.Encrypt};" +
            $"TrustServerCertificate={ConnectionSettings.TrustServerCertificate};";
        var conn = new SqlConnection(connectionString);
        return conn;
    }

    public override string GetTableStatement()
    {
        return "select table_name as name from information_schema.tables where table_type = 'BASE TABLE'";
    }

    public override string GetViewStatement()
    {
        return "select table_name as name from information_schema.views order by table_name";
    }
    
    public override string GetColumnStatement()
    {
        var sb = new StringBuilder();

        sb.AppendLine("SELECT ");
        sb.AppendLine("    c.name AS [Name],");
        sb.AppendLine("    t.name AS DataType,");
        sb.AppendLine("    CASE ");
        sb.AppendLine("        WHEN t.name IN ('nvarchar', 'nchar') AND c.max_length > 0 ");
        sb.AppendLine("            THEN CAST(c.max_length / 2 AS VARCHAR)");
        sb.AppendLine("        WHEN t.name IN ('varchar', 'char', 'varbinary') AND c.max_length > 0 ");
        sb.AppendLine("            THEN CAST(c.max_length AS VARCHAR)");
        sb.AppendLine("        ELSE CAST(c.max_length AS VARCHAR)");
        sb.AppendLine("    END AS Length,");
        sb.AppendLine("    c.precision AS Precision,");
        sb.AppendLine("    c.scale AS Scale,");
        sb.AppendLine("    c.is_nullable as IsNullable,");
        sb.AppendLine("    CASE WHEN k.column_id IS NOT NULL THEN 1 ELSE 0 END AS IsPrimaryKey");
        sb.AppendLine("FROM ");
        sb.AppendLine("    sys.columns c");
        sb.AppendLine("JOIN ");
        sb.AppendLine("    sys.types t ON c.user_type_id = t.user_type_id");
        sb.AppendLine("LEFT JOIN (");
        sb.AppendLine("    SELECT ic.object_id, ic.column_id");
        sb.AppendLine("    FROM sys.indexes i");
        sb.AppendLine("    INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id");
        sb.AppendLine("    WHERE i.is_primary_key = 1");
        sb.AppendLine(") k ON c.object_id = k.object_id AND c.column_id = k.column_id");
        sb.AppendLine("WHERE ");
        sb.AppendLine("    c.object_id = OBJECT_ID(@TableName)");
        sb.AppendLine("ORDER BY ");
        sb.AppendLine("    c.column_id;");
        
        return sb.ToString();
    }

    public override string GetProcedureStatement()
    {
        return """
               select
                   routine_name as Name
               from information_schema.routines
               where routine_type = 'PROCEDURE'
               order by routine_name;
               """;
    }

    public override string GetFunctionStatement()
    {
        return """
               select
                   routine_name as Name,
                   data_type as DataType
               from information_schema.routines
               where routine_type = 'FUNCTION'
               order by routine_name;
               """;
    }

    public override string GetRoutineParameterStatement()
    {
        return """
               select
                   coalesce(p.parameter_name, '@RETURN_VALUE') as Name,
                   p.data_type as DataType,
                   coalesce(p.character_maximum_length, 0) as Length,
                   coalesce(p.numeric_precision, 0) as Precision,
                   coalesce(p.numeric_scale, 0) as Scale,
                   cast(0 as bit) as IsNullable,
                   p.parameter_mode as Mode,
                   p.ordinal_position as Position
               from information_schema.parameters p
               where p.specific_name = @SpecificName
               order by p.ordinal_position;
               """;
    }
}
