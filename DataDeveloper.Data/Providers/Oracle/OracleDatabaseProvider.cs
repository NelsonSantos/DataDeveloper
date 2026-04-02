using System.Data.Common;
using System.Text;
using DataDeveloper.Data.Services;
using Oracle.ManagedDataAccess.Client;

namespace DataDeveloper.Data.Providers.Oracle;

public class OracleDatabaseProvider : DatabaseProviderBase<OracleConnectionSettings>
{
    public OracleDatabaseProvider(OracleConnectionSettings connectionSettings)
        : base(connectionSettings)
    {
    }

    public override DbConnection GetConnection()
    {
        var connectionStringBuilder = new OracleConnectionStringBuilder
        {
            UserID = ConnectionSettings.User,
            Password = ConnectionSettings.Password,
            DataSource = $"{ConnectionSettings.Server}:{ConnectionSettings.Port}/{ConnectionSettings.Database}"
        };

        return new OracleConnection(connectionStringBuilder.ConnectionString);
    }

    public override string GetTableStatement()
    {
        return """
               select table_name as Name
               from user_tables
               order by table_name
               """;
    }

    public override string GetViewStatement()
    {
        return """
               select view_name as Name
               from user_views
               order by view_name
               """;
    }

    public override string GetColumnStatement()
    {
        var sb = new StringBuilder();

        sb.AppendLine("select");
        sb.AppendLine("    c.column_name as Name,");
        sb.AppendLine("    lower(c.data_type) as DataType,");
        sb.AppendLine("    case");
        sb.AppendLine("        when c.char_col_decl_length is not null then c.char_col_decl_length");
        sb.AppendLine("        when c.data_length is not null then c.data_length");
        sb.AppendLine("        else 0");
        sb.AppendLine("    end as Length,");
        sb.AppendLine("    coalesce(c.data_precision, 0) as Precision,");
        sb.AppendLine("    coalesce(c.data_scale, 0) as Scale,");
        sb.AppendLine("    case when c.nullable = 'Y' then 1 else 0 end as IsNullable,");
        sb.AppendLine("    case when pk.column_name is not null then 1 else 0 end as IsPrimaryKey");
        sb.AppendLine("from user_tab_columns c");
        sb.AppendLine("left join (");
        sb.AppendLine("    select ucc.table_name, ucc.column_name");
        sb.AppendLine("    from user_constraints uc");
        sb.AppendLine("    join user_cons_columns ucc on ucc.constraint_name = uc.constraint_name");
        sb.AppendLine("    where uc.constraint_type = 'P'");
        sb.AppendLine(") pk on pk.table_name = c.table_name and pk.column_name = c.column_name");
        sb.AppendLine("where c.table_name = upper(:TableName)");
        sb.AppendLine("order by c.column_id");

        return sb.ToString();
    }

    public override string GetProcedureStatement()
    {
        return """
               select object_name as Name,
                      object_name as SpecificName
               from user_objects
               where object_type = 'PROCEDURE'
               order by object_name
               """;
    }

    public override string GetFunctionStatement()
    {
        return """
               select object_name as Name,
                      object_name as SpecificName,
                      null as DataType
               from user_objects
               where object_type = 'FUNCTION'
               order by object_name
               """;
    }

    public override string GetRoutineParameterStatement()
    {
        return """
               select
                   ':' || lower(argument_name) as "Name",
                   lower(data_type) as "DataType",
                   coalesce(char_length, data_length, 0) as "Length",
                   coalesce(data_precision, 0) as "Precision",
                   coalesce(data_scale, 0) as "Scale",
                   cast(1 as number(1)) as "IsNullable",
                   in_out as "Mode",
                   position as "Position"
               from user_arguments
               where object_name = upper(:SpecificName)
                 and package_name is null
                 and argument_name is not null
                 and data_level = 0
               order by sequence
               """;
    }
}
