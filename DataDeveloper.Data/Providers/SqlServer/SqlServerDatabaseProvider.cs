using System.Data;
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

    public override IDbConnection GetConnection()
    {
        ConnectionSettings.UseTrustedConnection = true;
        var connectionString = $"Server={ConnectionSettings.Server};Database={ConnectionSettings.Database};User Id={ConnectionSettings.User};Password={ConnectionSettings.Password};TrustServerCertificate={ConnectionSettings.UseTrustedConnection};";
        var conn = new SqlConnection(connectionString);
        return conn;
    }

    public override string GetTableStatement()
    {
        return "select table_name as name from information_schema.tables where table_type = 'BASE TABLE'";
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
        sb.AppendLine("        WHEN c.max_length = -1 ");
        sb.AppendLine("            THEN 'MAX'");
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
}