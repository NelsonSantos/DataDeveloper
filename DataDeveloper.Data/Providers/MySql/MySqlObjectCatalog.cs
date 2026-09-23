using System.Text;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Services.Metadata;

namespace DataDeveloper.Data.Providers.MySql;

public sealed class MySqlObjectCatalog : ObjectCatalog
{
    public MySqlObjectCatalog()
        : base(DatabaseType.MySql)
    {
    }

    protected override DdlRetrieval GetTableDdlRetrieval(DbObjectRef table)
    {
        return new DdlRetrieval($"show create table {QuoteQualifiedName(table)};");
    }

    protected override DdlRetrieval GetViewDdlRetrieval(DbObjectRef view)
    {
        return new DdlRetrieval($"show create view {QuoteQualifiedName(view)};");
    }

    protected override DdlRetrieval GetRoutineDdlRetrieval(DbObjectRef routine, bool isFunction)
    {
        return new DdlRetrieval($"show create {(isFunction ? "function" : "procedure")} {QuoteQualifiedName(routine)};");
    }

    public override string GetColumnDefaultsStatement()
    {
        return """
               select
                   column_name as ColumnName,
                   column_default as DefaultValueExpression
               from information_schema.columns
               where table_schema = coalesce(@SchemaName, database())
                 and table_name = @TableName
               order by ordinal_position;
               """;
    }

    public override string GetPrimaryKeyStatement()
    {
        return """
               select
                   k.constraint_name as ConstraintName,
                   k.column_name as ColumnName,
                   k.ordinal_position as OrdinalPosition
               from information_schema.key_column_usage k
               where k.table_schema = coalesce(@SchemaName, database())
                 and k.table_name = @TableName
                 and k.constraint_name = 'PRIMARY'
               order by k.ordinal_position;
               """;
    }

    public override string GetForeignKeysStatement()
    {
        return """
               select
                   k.constraint_name as ConstraintName,
                   k.column_name as ColumnName,
                   k.ordinal_position as OrdinalPosition,
                   k.referenced_table_schema as ReferencedSchemaName,
                   k.referenced_table_name as ReferencedTableName,
                   k.referenced_column_name as ReferencedColumnName,
                   lower(rc.delete_rule) as OnDeleteAction,
                   lower(rc.update_rule) as OnUpdateAction
               from information_schema.key_column_usage k
               join information_schema.referential_constraints rc
                   on rc.constraint_schema = k.table_schema
                  and rc.constraint_name = k.constraint_name
               where k.table_schema = coalesce(@SchemaName, database())
                 and k.table_name = @TableName
                 and k.referenced_table_name is not null
               order by k.constraint_name, k.ordinal_position;
               """;
    }

    public override string GetIndexesStatement()
    {
        return """
               select
                   index_name as IndexName,
                   case when non_unique = 0 then 1 else 0 end as IsUnique,
                   column_name as ColumnName,
                   case when collation = 'D' then 1 else 0 end as IsDescending,
                   seq_in_index as OrdinalPosition,
                   sub_part as PrefixLength
               from information_schema.statistics
               where table_schema = coalesce(@SchemaName, database())
                 and table_name = @TableName
                 and index_name <> 'PRIMARY'
               order by index_name, seq_in_index;
               """;
    }

    public override string GetObjectListStatement(DbObjectKind kind)
    {
        return kind switch
        {
            DbObjectKind.Table => """
                                  select table_schema as SchemaName, table_name as Name, 1 as IsDefaultSchema
                                  from information_schema.tables
                                  where table_schema = database()
                                    and table_type = 'BASE TABLE';
                                  """,
            DbObjectKind.View => """
                                 select table_schema as SchemaName, table_name as Name, 1 as IsDefaultSchema
                                 from information_schema.views
                                 where table_schema = database();
                                 """,
            DbObjectKind.Procedure or DbObjectKind.Function => $"""
                select
                    routine_schema as SchemaName,
                    routine_name as Name,
                    1 as IsDefaultSchema,
                    specific_name as SpecificName,
                    data_type as DataType
                from information_schema.routines
                where routine_schema = database()
                  and routine_type = '{(kind == DbObjectKind.Procedure ? "PROCEDURE" : "FUNCTION")}';
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    public override string GetColumnsStatement()
    {
        var sb = new StringBuilder();

        sb.AppendLine("select");
        sb.AppendLine("    c.column_name as `Name`,");
        sb.AppendLine("    c.data_type as `DataType`,");
        sb.AppendLine("    c.character_maximum_length as `Length`,");
        sb.AppendLine("    c.numeric_precision as `Precision`,");
        sb.AppendLine("    c.numeric_scale as `Scale`,");
        sb.AppendLine("    case when c.is_nullable = 'YES' then 1 else 0 end as `IsNullable`,");
        sb.AppendLine("    case when k.column_name is not null then 1 else 0 end as `IsPrimaryKey`,");
        sb.AppendLine("    case when c.extra like '%auto_increment%' then 1 else 0 end as `IsAutoGenerated`,");
        sb.AppendLine("    case when c.column_default is not null then 1 else 0 end as `HasDefaultValue`");
        sb.AppendLine("from information_schema.columns c");
        sb.AppendLine("left join information_schema.key_column_usage k");
        sb.AppendLine("    on k.table_schema = c.table_schema");
        sb.AppendLine("   and k.table_name = c.table_name");
        sb.AppendLine("   and k.column_name = c.column_name");
        sb.AppendLine("   and k.constraint_name = 'PRIMARY'");
        sb.AppendLine("where c.table_schema = coalesce(@SchemaName, database())");
        sb.AppendLine("  and c.table_name = @TableName");
        sb.AppendLine("order by c.ordinal_position;");

        return sb.ToString();
    }


    public override string GetRoutineParametersStatement()
    {
        return """
               select
                   coalesce(parameter_name, 'return') as Name,
                   data_type as DataType,
                   coalesce(character_maximum_length, 0) as Length,
                   coalesce(numeric_precision, 0) as `Precision`,
                   coalesce(numeric_scale, 0) as `Scale`,
                   cast(1 as unsigned) as `IsNullable`,
                   parameter_mode as `Mode`,
                   ordinal_position as `Position`
               from information_schema.parameters
               where specific_schema = database()
                 and specific_name = @SpecificName
               order by ordinal_position;
               """;
    }
}
