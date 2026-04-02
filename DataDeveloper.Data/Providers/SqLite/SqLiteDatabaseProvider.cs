using System.Data.Common;
using System.Text;
using DataDeveloper.Data.Services;
using Microsoft.Data.Sqlite;

namespace DataDeveloper.Data.Providers.SqLite;

public class SqLiteDatabaseProvider : DatabaseProviderBase<SqLiteConnectionSettings>
{
    public SqLiteDatabaseProvider(SqLiteConnectionSettings connectionSettings)
        : base(connectionSettings)
    {
    }

    public override DbConnection GetConnection()
    {
        var connectionStringBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = ConnectionSettings.Database
        };

        return new SqliteConnection(connectionStringBuilder.ConnectionString);
    }

    public override string GetTableStatement()
    {
        return """
               select name as Name
               from sqlite_master
               where type = 'table'
                 and name not like 'sqlite_%'
               order by name
               """;
    }

    public override string GetViewStatement()
    {
        return """
               select name as Name
               from sqlite_master
               where type = 'view'
               order by name
               """;
    }

    public override string GetColumnStatement()
    {
        var sb = new StringBuilder();

        sb.AppendLine("select");
        sb.AppendLine("    p.name as Name,");
        sb.AppendLine("    lower(p.type) as DataType,");
        sb.AppendLine("    0 as Length,");
        sb.AppendLine("    0 as Precision,");
        sb.AppendLine("    0 as Scale,");
        sb.AppendLine("    case when p.\"notnull\" = 0 then 1 else 0 end as IsNullable,");
        sb.AppendLine("    case when p.pk > 0 then 1 else 0 end as IsPrimaryKey");
        sb.AppendLine("from pragma_table_info(__table_name__) p");
        sb.AppendLine("order by p.cid");

        return sb.ToString();
    }

    public override string GetProcedureStatement()
    {
        return """
               select null as Name, null as SpecificName
               where 1 = 0
               """;
    }

    public override string GetFunctionStatement()
    {
        return """
               select null as Name, null as SpecificName, null as DataType
               where 1 = 0
               """;
    }

    public override string GetRoutineParameterStatement()
    {
        return """
               select null as Name, null as DataType, 0 as Length, 0 as Precision, 0 as Scale, 1 as IsNullable, null as Mode, 0 as Position
               where 1 = 0
               """;
    }
}
