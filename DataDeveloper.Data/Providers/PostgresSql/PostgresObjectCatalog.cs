using System.Text;
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

    public override string GetColumnDefaultsStatement()
    {
        return """
               select
                   column_name as "ColumnName",
                   column_default as "DefaultValueExpression"
               from information_schema.columns
               where table_schema = coalesce(cast(@SchemaName as text), current_schema())
                 and table_name = @TableName
               order by ordinal_position;
               """;
    }

    public override string GetPrimaryKeyStatement()
    {
        return """
               select
                   tc.constraint_name as "ConstraintName",
                   kcu.column_name as "ColumnName",
                   kcu.ordinal_position as "OrdinalPosition"
               from information_schema.table_constraints tc
               join information_schema.key_column_usage kcu
                   on kcu.constraint_schema = tc.constraint_schema
                  and kcu.constraint_name = tc.constraint_name
               where tc.table_schema = coalesce(cast(@SchemaName as text), current_schema())
                 and tc.table_name = @TableName
                 and tc.constraint_type = 'PRIMARY KEY'
               order by kcu.ordinal_position;
               """;
    }

    public override string GetForeignKeysStatement()
    {
        return """
               select
                   con.conname as "ConstraintName",
                   att2.attname as "ColumnName",
                   ord.ordinality as "OrdinalPosition",
                   rn.nspname as "ReferencedSchemaName",
                   rc.relname as "ReferencedTableName",
                   att1.attname as "ReferencedColumnName",
                   case con.confdeltype
                       when 'c' then 'cascade' when 'n' then 'set null'
                       when 'd' then 'set default' when 'r' then 'restrict' else '' end as "OnDeleteAction",
                   case con.confupdtype
                       when 'c' then 'cascade' when 'n' then 'set null'
                       when 'd' then 'set default' when 'r' then 'restrict' else '' end as "OnUpdateAction"
               from pg_constraint con
               join pg_class t on t.oid = con.conrelid
               join pg_namespace n on n.oid = t.relnamespace
               join pg_class rc on rc.oid = con.confrelid
               join pg_namespace rn on rn.oid = rc.relnamespace
               cross join lateral unnest(con.conkey, con.confkey) with ordinality as ord(local_attnum, ref_attnum, ordinality)
               join pg_attribute att2 on att2.attrelid = con.conrelid and att2.attnum = ord.local_attnum
               join pg_attribute att1 on att1.attrelid = con.confrelid and att1.attnum = ord.ref_attnum
               where con.contype = 'f'
                 and n.nspname = coalesce(cast(@SchemaName as text), current_schema())
                 and t.relname = @TableName
               order by con.conname, ord.ordinality;
               """;
    }

    public override string GetIndexesStatement()
    {
        return """
               select
                   ic.relname as "IndexName",
                   i.indisunique as "IsUnique",
                   a.attname as "ColumnName",
                   (o.option & 1) = 1 as "IsDescending",
                   o.ordinality as "OrdinalPosition",
                   am.amname as "UsingMethod",
                   pg_get_expr(i.indpred, i.indrelid) as "WherePredicate"
               from pg_index i
               join pg_class t on t.oid = i.indrelid
               join pg_namespace n on n.oid = t.relnamespace
               join pg_class ic on ic.oid = i.indexrelid
               join pg_am am on am.oid = ic.relam
               cross join lateral unnest(i.indkey, i.indoption) with ordinality as o(attnum, option, ordinality)
               join pg_attribute a on a.attrelid = t.oid and a.attnum = o.attnum
               where n.nspname = coalesce(cast(@SchemaName as text), current_schema())
                 and t.relname = @TableName
                 and not i.indisprimary
                 and not exists (
                     select 1 from pg_constraint con
                     where con.conrelid = t.oid and con.conindid = i.indexrelid
                 )
               order by ic.relname, o.ordinality;
               """;
    }

    public override string GetObjectListStatement(DbObjectKind kind)
    {
        return kind switch
        {
            DbObjectKind.Table => """
                                  select table_schema as SchemaName, table_name as Name, true as IsDefaultSchema
                                  from information_schema.tables
                                  where table_schema = current_schema()
                                    and table_type = 'BASE TABLE';
                                  """,
            DbObjectKind.View => """
                                 select table_schema as SchemaName, table_name as Name, true as IsDefaultSchema
                                 from information_schema.views
                                 where table_schema = current_schema();
                                 """,
            DbObjectKind.Procedure or DbObjectKind.Function => $"""
                select
                    routine_schema as SchemaName,
                    routine_name as Name,
                    true as IsDefaultSchema,
                    specific_name as SpecificName,
                    data_type as DataType
                from information_schema.routines
                where specific_schema = current_schema()
                  and routine_type = '{(kind == DbObjectKind.Procedure ? "PROCEDURE" : "FUNCTION")}';
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    public override string GetColumnsStatement()
    {
        var sb = new StringBuilder();

        // IsPrimaryKey is computed with an EXISTS subquery scoped to PRIMARY KEY constraints
        // only. A join against key_column_usage without that scoping (as this used to do)
        // also matches foreign-key columns, since key_column_usage lists every constraint
        // type, not just primary keys - that previously caused the WHERE filter to silently
        // drop foreign-key columns from the result entirely instead of just mis-flagging them.
        sb.AppendLine("select");
        sb.AppendLine("    c.column_name as \"Name\",");
        sb.AppendLine("    c.data_type as \"DataType\",");
        sb.AppendLine("    coalesce(c.character_maximum_length, 0) as \"Length\",");
        sb.AppendLine("    coalesce(c.numeric_precision, 0) as \"Precision\",");
        sb.AppendLine("    coalesce(c.numeric_scale, 0) as \"Scale\",");
        sb.AppendLine("    case when c.is_nullable = 'YES' then true else false end as \"IsNullable\",");
        sb.AppendLine("    exists (");
        sb.AppendLine("        select 1");
        sb.AppendLine("        from information_schema.table_constraints tc");
        sb.AppendLine("        join information_schema.key_column_usage kcu");
        sb.AppendLine("            on kcu.constraint_schema = tc.constraint_schema");
        sb.AppendLine("           and kcu.constraint_name = tc.constraint_name");
        sb.AppendLine("        where tc.table_schema = c.table_schema");
        sb.AppendLine("          and tc.table_name = c.table_name");
        sb.AppendLine("          and tc.constraint_type = 'PRIMARY KEY'");
        sb.AppendLine("          and kcu.column_name = c.column_name");
        sb.AppendLine("    ) as \"IsPrimaryKey\",");
        sb.AppendLine("    case when c.is_identity = 'YES' or c.is_generated = 'ALWAYS' then true else false end as \"IsAutoGenerated\",");
        sb.AppendLine("    case when c.column_default is not null then true else false end as \"HasDefaultValue\"");
        sb.AppendLine("from information_schema.columns c");
        sb.AppendLine("where c.table_schema = coalesce(cast(@SchemaName as text), current_schema())");
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

    public override string GetUniqueConstraintsStatement()
    {
        return """
               select
                   con.conname as "ConstraintName",
                   a.attname as "ColumnName",
                   ord.ordinality as "OrdinalPosition"
               from pg_constraint con
               join pg_class t on t.oid = con.conrelid
               join pg_namespace n on n.oid = t.relnamespace
               cross join lateral unnest(con.conkey) with ordinality as ord(attnum, ordinality)
               join pg_attribute a on a.attrelid = con.conrelid and a.attnum = ord.attnum
               where con.contype = 'u'
                 and n.nspname = coalesce(cast(@SchemaName as text), current_schema())
                 and t.relname = @TableName
               order by con.conname, ord.ordinality;
               """;
    }

    public override CheckConstraintsQuery? GetCheckConstraintsQuery(string serverVersion)
    {
        return new CheckConstraintsQuery("""
            select
                con.conname as "ConstraintName",
                pg_get_expr(con.conbin, con.conrelid) as "Definition"
            from pg_constraint con
            join pg_class t on t.oid = con.conrelid
            join pg_namespace n on n.oid = t.relnamespace
            where con.contype = 'c'
              and n.nspname = coalesce(cast(@SchemaName as text), current_schema())
              and t.relname = @TableName
            order by con.conname;
            """);
    }

    // Trigger names are unique per table, not per schema, so the table narrows the lookup.
    protected override DdlRetrieval GetTriggerDdlRetrieval(DbObjectRef trigger)
    {
        var schemaName = trigger.Parent?.Schema ?? trigger.Schema ?? DefaultSchema;
        var tablePredicate = trigger.Parent is null
            ? string.Empty
            : $"{Environment.NewLine}  and c.relname = '{EscapeSqlLiteral(trigger.Parent.Name)}'";

        return new DdlRetrieval(
            "select pg_get_triggerdef(t.oid, true) || ';' as Definition" + Environment.NewLine +
            "from pg_trigger t" + Environment.NewLine +
            "join pg_class c on c.oid = t.tgrelid" + Environment.NewLine +
            "join pg_namespace n on n.oid = c.relnamespace" + Environment.NewLine +
            "where not t.tgisinternal" + Environment.NewLine +
            $"  and n.nspname = '{EscapeSqlLiteral(schemaName)}'" +
            tablePredicate + Environment.NewLine +
            $"  and t.tgname = '{EscapeSqlLiteral(trigger.Name)}';");
    }

    public override TriggersQuery GetTriggersQuery()
    {
        return new TriggersQuery("""
            select
                trigger_schema as "SchemaName",
                trigger_name as "Name",
                lower(action_timing) as "Timing",
                string_agg(lower(event_manipulation), ', ' order by event_manipulation) as "Events"
            from information_schema.triggers
            where event_object_schema = coalesce(cast(@SchemaName as text), current_schema())
              and event_object_table = @TableName
            group by trigger_schema, trigger_name, action_timing
            order by trigger_name;
            """);
    }
}
