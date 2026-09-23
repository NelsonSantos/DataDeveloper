using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Services.Metadata;

namespace DataDeveloper.Data.Providers.SqLite;

public sealed class SqLiteObjectCatalog : ObjectCatalog
{
    public SqLiteObjectCatalog()
        : base(DatabaseType.SqLite)
    {
    }

    protected override DdlRetrieval GetTableDdlRetrieval(DbObjectRef table)
    {
        return new DdlRetrieval(BuildSqliteMasterQuery("table", table.Name));
    }

    protected override DdlRetrieval GetViewDdlRetrieval(DbObjectRef view)
    {
        return new DdlRetrieval(BuildSqliteMasterQuery("view", view.Name));
    }

    // SQLite has no stored procedures or functions.
    protected override DdlRetrieval? GetRoutineDdlRetrieval(DbObjectRef routine, bool isFunction) => null;

    private static string BuildSqliteMasterQuery(string type, string name)
    {
        return "select sql as Definition" + Environment.NewLine +
               "from sqlite_master" + Environment.NewLine +
               $"where type = '{type}' and name = '{EscapeSqlLiteral(name)}';";
    }

    // Only the main database is listed in the tree, so the SchemaName parameter is not used.

    public override string GetColumnDefaultsStatement()
    {
        return """
               select
                   p.name as ColumnName,
                   p.dflt_value as DefaultValueExpression
               from pragma_table_info(@TableName) p
               order by p.cid
               """;
    }

    public override string GetPrimaryKeyStatement()
    {
        // SQLite has no named PK constraint.
        return """
               select
                   '' as ConstraintName,
                   p.name as ColumnName,
                   p.pk as OrdinalPosition
               from pragma_table_info(@TableName) p
               where p.pk > 0
               order by p.pk
               """;
    }

    public override string GetForeignKeysStatement()
    {
        // SQLite has no named FK constraint; synthesize a stable name from the FK group id
        // so multiple columns of the same foreign key are grouped consistently by the loader.
        return """
               select
                   'fk_' || fk.id as ConstraintName,
                   fk."from" as ColumnName,
                   fk.seq as OrdinalPosition,
                   '' as ReferencedSchemaName,
                   fk."table" as ReferencedTableName,
                   fk."to" as ReferencedColumnName,
                   lower(fk.on_delete) as OnDeleteAction,
                   lower(fk.on_update) as OnUpdateAction
               from pragma_foreign_key_list(@TableName) fk
               order by fk.id, fk.seq
               """;
    }

    public override string GetIndexesStatement()
    {
        return """
               select
                   il.name as IndexName,
                   il."unique" as IsUnique,
                   ix.name as ColumnName,
                   ix.desc as IsDescending,
                   ix.seqno as OrdinalPosition
               from pragma_index_list(@TableName) il
               join pragma_index_xinfo(il.name) ix on ix.key = 1
               where il.origin = 'c'
               order by il.name, ix.seqno
               """;
    }
}
