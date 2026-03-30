using System.Data.Common;
using System.Text;
using DataDeveloper.Data.Services;
using MySqlConnector;

namespace DataDeveloper.Data.Providers.MySql;

public class MySqlDatabaseProvider : DatabaseProviderBase<MySqlConnectionSettings>
{
    public MySqlDatabaseProvider(MySqlConnectionSettings connectionSettings)
        : base(connectionSettings)
    {
    }

    public override DbConnection GetConnection()
    {
        var sslMode = ConnectionSettings.Encrypt
            ? ConnectionSettings.TrustServerCertificate
                ? MySqlSslMode.Required
                : MySqlSslMode.VerifyCA
            : MySqlSslMode.None;

        var connectionStringBuilder = new MySqlConnectionStringBuilder
        {
            Server = ConnectionSettings.Server,
            Database = ConnectionSettings.Database,
            UserID = ConnectionSettings.User,
            Password = ConnectionSettings.Password,
            Port = ConnectionSettings.Port,
            SslMode = sslMode,
            AllowPublicKeyRetrieval = true
        };

        return new MySqlConnection(connectionStringBuilder.ConnectionString);
    }

    public override string GetTableStatement()
    {
        return """
               select table_name as name
               from information_schema.tables
               where table_schema = database()
                 and table_type = 'BASE TABLE'
               order by table_name;
               """;
    }

    public override string GetViewStatement()
    {
        return """
               select table_name as name
               from information_schema.views
               where table_schema = database()
               order by table_name;
               """;
    }

    public override string GetColumnStatement()
    {
        var sb = new StringBuilder();

        sb.AppendLine("select");
        sb.AppendLine("    c.column_name as `Name`,");
        sb.AppendLine("    c.data_type as `DataType`,");
        sb.AppendLine("    c.character_maximum_length as `Length`,");
        sb.AppendLine("    c.numeric_precision as `Precision`,");
        sb.AppendLine("    c.numeric_scale as `Scale`,");
        sb.AppendLine("    case when c.is_nullable = 'YES' then 1 else 0 end as `IsNullable`,");
        sb.AppendLine("    case when k.column_name is not null then 1 else 0 end as `IsPrimaryKey`");
        sb.AppendLine("from information_schema.columns c");
        sb.AppendLine("left join information_schema.key_column_usage k");
        sb.AppendLine("    on k.table_schema = c.table_schema");
        sb.AppendLine("   and k.table_name = c.table_name");
        sb.AppendLine("   and k.column_name = c.column_name");
        sb.AppendLine("   and k.constraint_name = 'PRIMARY'");
        sb.AppendLine("where c.table_schema = database()");
        sb.AppendLine("  and c.table_name = @TableName");
        sb.AppendLine("order by c.ordinal_position;");

        return sb.ToString();
    }

    public override string GetProcedureStatement()
    {
        return """
               select
                   routine_name as Name,
                   specific_name as SpecificName
               from information_schema.routines
               where routine_schema = database()
                 and routine_type = 'PROCEDURE'
               order by routine_name;
               """;
    }

    public override string GetFunctionStatement()
    {
        return """
               select
                   routine_name as Name,
                   specific_name as SpecificName,
                   data_type as DataType
               from information_schema.routines
               where routine_schema = database()
                 and routine_type = 'FUNCTION'
               order by routine_name;
               """;
    }

    public override string GetRoutineParameterStatement()
    {
        return """
               select
                   coalesce(parameter_name, 'return') as Name,
                   data_type as DataType,
                   coalesce(character_maximum_length, 0) as Length,
                   coalesce(numeric_precision, 0) as Precision,
                   coalesce(numeric_scale, 0) as Scale,
                   case when is_nullable = 'YES' then 1 else 0 end as IsNullable,
                   parameter_mode as Mode,
                   ordinal_position as Position
               from information_schema.parameters
               where specific_schema = database()
                 and specific_name = @SpecificName
               order by ordinal_position;
               """;
    }
}
