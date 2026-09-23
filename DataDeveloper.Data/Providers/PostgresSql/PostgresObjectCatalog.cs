using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Services.Metadata;

namespace DataDeveloper.Data.Providers.PostgresSql;

public sealed class PostgresObjectCatalog : ObjectCatalog
{
    private const string DefaultSchema = "public";

    public PostgresObjectCatalog()
        : base(DatabaseType.PostgresSql)
    {
    }

    protected override DdlRetrieval GetTableDdlRetrieval(DbObjectRef table)
    {
        return new DdlRetrieval(BuildTableDdlQuery(table.Schema ?? DefaultSchema, table.Name));
    }

    protected override DdlRetrieval GetViewDdlRetrieval(DbObjectRef view)
    {
        return new DdlRetrieval(
            "select 'create or replace view ' || quote_ident(n.nspname) || '.' || quote_ident(c.relname) || ' as' || E'\\n' || pg_get_viewdef(c.oid, true) as Definition" + Environment.NewLine +
            "from pg_class c" + Environment.NewLine +
            "join pg_namespace n on n.oid = c.relnamespace" + Environment.NewLine +
            $"where n.nspname = '{EscapeSqlLiteral(view.Schema ?? DefaultSchema)}'" + Environment.NewLine +
            $"  and c.relname = '{EscapeSqlLiteral(view.Name)}'" + Environment.NewLine +
            "  and c.relkind = 'v';");
    }

    protected override DdlRetrieval GetRoutineDdlRetrieval(DbObjectRef routine, bool isFunction)
    {
        return new DdlRetrieval(
            $"select pg_get_functiondef(p.oid){Environment.NewLine}" +
            "from pg_proc p" + Environment.NewLine +
            "join pg_namespace n on n.oid = p.pronamespace" + Environment.NewLine +
            $"where n.nspname = '{EscapeSqlLiteral(routine.Schema ?? DefaultSchema)}'" + Environment.NewLine +
            $"  and p.proname = '{EscapeSqlLiteral(routine.Name)}';");
    }

    private static string BuildTableDdlQuery(string schemaName, string tableName)
    {
        var escapedSchemaName = EscapeSqlLiteral(schemaName);
        var escapedTableName = EscapeSqlLiteral(tableName);

        return
            "with target_table as (" + Environment.NewLine +
            "    select c.oid, n.nspname as schema_name, c.relname as table_name" + Environment.NewLine +
            "    from pg_class c" + Environment.NewLine +
            "    join pg_namespace n on n.oid = c.relnamespace" + Environment.NewLine +
            $"    where n.nspname = '{escapedSchemaName}'" + Environment.NewLine +
            $"      and c.relname = '{escapedTableName}'" + Environment.NewLine +
            "      and c.relkind in ('r', 'p')" + Environment.NewLine +
            "), column_data as (" + Environment.NewLine +
            "    select" + Environment.NewLine +
            "        a.attrelid," + Environment.NewLine +
            "        a.attnum," + Environment.NewLine +
            "        a.attname," + Environment.NewLine +
            "        a.attnotnull," + Environment.NewLine +
            "        a.attidentity," + Environment.NewLine +
            "        a.attgenerated," + Environment.NewLine +
            "        format_type(a.atttypid, a.atttypmod) as formatted_type," + Environment.NewLine +
            "        pg_get_expr(ad.adbin, ad.adrelid) as default_expr," + Environment.NewLine +
            "        replace(replace(replace(format_type(a.atttypid, a.atttypmod), 'character varying', 'varchar'), 'timestamp without time zone', 'timestamp'), 'time without time zone', 'time') as display_type," + Environment.NewLine +
            "        pg_get_serial_sequence(format('%I.%I', tt.schema_name, tt.table_name), a.attname) as serial_sequence," + Environment.NewLine +
            "        (" + Environment.NewLine +
            "            a.attidentity = '' and" + Environment.NewLine +
            "            a.attgenerated = '' and" + Environment.NewLine +
            "            pg_get_serial_sequence(format('%I.%I', tt.schema_name, tt.table_name), a.attname) is not null and" + Environment.NewLine +
            "            pg_get_expr(ad.adbin, ad.adrelid) = 'nextval(''' || pg_get_serial_sequence(format('%I.%I', tt.schema_name, tt.table_name), a.attname) || '''::regclass)'" + Environment.NewLine +
            "        ) as is_schema_qualified_serial," + Environment.NewLine +
            "        (" + Environment.NewLine +
            "            a.attidentity = '' and" + Environment.NewLine +
            "            a.attgenerated = '' and" + Environment.NewLine +
            "            pg_get_serial_sequence(format('%I.%I', tt.schema_name, tt.table_name), a.attname) is not null and" + Environment.NewLine +
            "            pg_get_expr(ad.adbin, ad.adrelid) = 'nextval(''' || split_part(pg_get_serial_sequence(format('%I.%I', tt.schema_name, tt.table_name), a.attname), '.', 2) || '''::regclass)'" + Environment.NewLine +
            "        ) as is_unqualified_serial" + Environment.NewLine +
            "    from target_table tt" + Environment.NewLine +
            "    join pg_attribute a on a.attrelid = tt.oid" + Environment.NewLine +
            "    left join pg_attrdef ad on ad.adrelid = a.attrelid and ad.adnum = a.attnum" + Environment.NewLine +
            "    where a.attnum > 0" + Environment.NewLine +
            "      and not a.attisdropped" + Environment.NewLine +
            "), table_ddl as (" + Environment.NewLine +
            "    select" + Environment.NewLine +
            "        'create table ' || quote_ident(tt.schema_name) || '.' || quote_ident(tt.table_name) || E'\\n(' || E'\\n' ||" + Environment.NewLine +
            "        (" + Environment.NewLine +
            "            select string_agg(" + Environment.NewLine +
            "                case when a.is_schema_qualified_serial or a.is_unqualified_serial then '    -- sequence: ' || a.serial_sequence || E'\\n' else '' end ||" + Environment.NewLine +
            "                '    ' || quote_ident(a.attname) || ' ' ||" + Environment.NewLine +
            "                case" + Environment.NewLine +
            "                    when a.attidentity in ('a', 'd')" + Environment.NewLine +
            "                        then a.display_type || ' generated ' ||" + Environment.NewLine +
            "                             case a.attidentity when 'a' then 'always' else 'by default' end || ' as identity'" + Environment.NewLine +
            "                    when a.is_schema_qualified_serial or a.is_unqualified_serial" + Environment.NewLine +
            "                        then case a.formatted_type" + Environment.NewLine +
            "                                when 'integer' then 'serial'" + Environment.NewLine +
            "                                when 'bigint' then 'bigserial'" + Environment.NewLine +
            "                                when 'smallint' then 'smallserial'" + Environment.NewLine +
            "                                else a.display_type" + Environment.NewLine +
            "                             end" + Environment.NewLine +
            "                    else a.display_type" + Environment.NewLine +
            "                end ||" + Environment.NewLine +
            "                case when a.attgenerated = 's' then ' generated always as (' || a.default_expr || ') stored' else '' end ||" + Environment.NewLine +
            "                case" + Environment.NewLine +
            "                    when a.attgenerated <> '' or a.attidentity in ('a', 'd') then ''" + Environment.NewLine +
            "                    when a.is_schema_qualified_serial or a.is_unqualified_serial then ''" + Environment.NewLine +
            "                    when a.default_expr is not null then ' default ' || a.default_expr" + Environment.NewLine +
            "                    else ''" + Environment.NewLine +
            "                end ||" + Environment.NewLine +
            "                case when a.attnotnull then ' not null' else '' end," + Environment.NewLine +
            "                ',' || E'\\n' ORDER BY a.attnum" + Environment.NewLine +
            "            )" + Environment.NewLine +
            "            from column_data a" + Environment.NewLine +
            "            where a.attrelid = tt.oid" + Environment.NewLine +
            "        ) ||" + Environment.NewLine +
            "        coalesce((" + Environment.NewLine +
            "            select E',\\n' || string_agg('    constraint ' || quote_ident(con.conname) || ' ' || replace(replace(pg_get_constraintdef(con.oid, true), 'PRIMARY KEY', 'primary key'), 'FOREIGN KEY', 'foreign key'), E',\\n' ORDER BY con.conname)" + Environment.NewLine +
            "            from pg_constraint con" + Environment.NewLine +
            "            where con.conrelid = tt.oid" + Environment.NewLine +
            "              and con.contype in ('p', 'u', 'f', 'c')" + Environment.NewLine +
            "        ), '') || E'\\n);' as definition," + Environment.NewLine +
            "        tt.oid" + Environment.NewLine +
            "    from target_table tt" + Environment.NewLine +
            "), index_ddl as (" + Environment.NewLine +
            "    select pg_get_indexdef(i.indexrelid) || ';' as definition" + Environment.NewLine +
            "    from target_table tt" + Environment.NewLine +
            "    join pg_index i on i.indrelid = tt.oid" + Environment.NewLine +
            "    join pg_class ic on ic.oid = i.indexrelid" + Environment.NewLine +
            "    where not i.indisprimary" + Environment.NewLine +
            "      and not exists (" + Environment.NewLine +
            "          select 1" + Environment.NewLine +
            "          from pg_constraint con" + Environment.NewLine +
            "          where con.conrelid = tt.oid" + Environment.NewLine +
            "            and con.conindid = i.indexrelid" + Environment.NewLine +
            "      )" + Environment.NewLine +
            "    order by ic.relname" + Environment.NewLine +
            ")" + Environment.NewLine +
            "select definition as Definition from table_ddl" + Environment.NewLine +
            "union all" + Environment.NewLine +
            "select definition as Definition from index_ddl;";
    }
}
