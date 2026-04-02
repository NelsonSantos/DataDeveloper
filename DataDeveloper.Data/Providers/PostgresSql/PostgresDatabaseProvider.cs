using System.Data.Common;
using System.Text;
using DataDeveloper.Data.Services;
using Npgsql;

namespace DataDeveloper.Data.Providers.PostgresSql;

public class PostgresDatabaseProvider : DatabaseProviderBase<PostgresConnectionSettings>
{
    public PostgresDatabaseProvider(PostgresConnectionSettings connectionSettings)
        : base(connectionSettings)
    {
    }

    public override DbConnection GetConnection()
    {
        var sslMode = ConnectionSettings.Encrypt
            ? ConnectionSettings.TrustServerCertificate
                ? SslMode.Require
                : SslMode.VerifyCA
            : SslMode.Disable;

        var connectionStringBuilder = new NpgsqlConnectionStringBuilder
        {
            Host = ConnectionSettings.Server,
            Database = ConnectionSettings.Database,
            Username = ConnectionSettings.User,
            Password = ConnectionSettings.Password,
            Port = ConnectionSettings.Port,
            SslMode = sslMode,
            TrustServerCertificate = ConnectionSettings.TrustServerCertificate
        };

        return new NpgsqlConnection(connectionStringBuilder.ConnectionString);
    }

    public override string GetTableStatement()
    {
        return """
               select table_name as Name
               from information_schema.tables
               where table_schema = current_schema()
                 and table_type = 'BASE TABLE'
               order by table_name;
               """;
    }

    public override string GetViewStatement()
    {
        return """
               select table_name as Name
               from information_schema.views
               where table_schema = current_schema()
               order by table_name;
               """;
    }

    public override string GetColumnStatement()
    {
        var sb = new StringBuilder();

        sb.AppendLine("select");
        sb.AppendLine("    c.column_name as \"Name\",");
        sb.AppendLine("    c.data_type as \"DataType\",");
        sb.AppendLine("    coalesce(c.character_maximum_length, 0) as \"Length\",");
        sb.AppendLine("    coalesce(c.numeric_precision, 0) as \"Precision\",");
        sb.AppendLine("    coalesce(c.numeric_scale, 0) as \"Scale\",");
        sb.AppendLine("    case when c.is_nullable = 'YES' then true else false end as \"IsNullable\",");
        sb.AppendLine("    case when k.column_name is not null then true else false end as \"IsPrimaryKey\"");
        sb.AppendLine("from information_schema.columns c");
        sb.AppendLine("left join information_schema.key_column_usage k");
        sb.AppendLine("    on k.table_schema = c.table_schema");
        sb.AppendLine("   and k.table_name = c.table_name");
        sb.AppendLine("   and k.column_name = c.column_name");
        sb.AppendLine("left join information_schema.table_constraints tc");
        sb.AppendLine("    on tc.constraint_schema = k.constraint_schema");
        sb.AppendLine("   and tc.constraint_name = k.constraint_name");
        sb.AppendLine("   and tc.table_name = k.table_name");
        sb.AppendLine("   and tc.constraint_type = 'PRIMARY KEY'");
        sb.AppendLine("where c.table_schema = current_schema()");
        sb.AppendLine("  and c.table_name = @TableName");
        sb.AppendLine("  and (k.column_name is null or tc.constraint_name is not null)");
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
               where specific_schema = current_schema()
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
               where specific_schema = current_schema()
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
                   true as IsNullable,
                   parameter_mode as Mode,
                   ordinal_position as Position
               from information_schema.parameters
               where specific_schema = current_schema()
                 and specific_name = @SpecificName
               order by ordinal_position;
               """;
    }
}
