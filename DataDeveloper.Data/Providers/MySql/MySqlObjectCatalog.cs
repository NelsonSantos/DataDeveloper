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
}
