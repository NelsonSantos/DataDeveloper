using System.Text;
using System.Text.RegularExpressions;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Services.Metadata;

namespace DataDeveloper.Data.Providers.Oracle;

public sealed class OracleObjectCatalog : ObjectCatalog
{
    private const string MetadataSessionSetup = """
                                                begin
                                                    dbms_metadata.set_transform_param(dbms_metadata.session_transform, 'PRETTY', true);
                                                    dbms_metadata.set_transform_param(dbms_metadata.session_transform, 'SQLTERMINATOR', true);
                                                    dbms_metadata.set_transform_param(dbms_metadata.session_transform, 'SEGMENT_ATTRIBUTES', false);
                                                    dbms_metadata.set_transform_param(dbms_metadata.session_transform, 'STORAGE', false);
                                                    dbms_metadata.set_transform_param(dbms_metadata.session_transform, 'TABLESPACE', false);
                                                end;
                                                """;

    public OracleObjectCatalog()
        : base(DatabaseType.Oracle)
    {
    }

    protected override DdlRetrieval GetTableDdlRetrieval(DbObjectRef table)
    {
        var owner = table.Schema is null
            ? "sys_context('USERENV', 'CURRENT_SCHEMA')"
            : $"'{EscapeSqlLiteral(table.Schema)}'";

        return new DdlRetrieval(
            $"select dbms_metadata.get_ddl('TABLE', '{EscapeSqlLiteral(table.Name)}', {owner}) as Definition from dual;",
            MetadataSessionSetup,
            ddl => BeautifyTableDdl(table, ddl));
    }

    protected override DdlRetrieval GetViewDdlRetrieval(DbObjectRef view)
    {
        return new DdlRetrieval(
            "select 'create or replace view ' || view_name || chr(10) || 'as' || chr(10) || text_vc as Definition" + Environment.NewLine +
            "from user_views" + Environment.NewLine +
            $"where view_name = '{EscapeSqlLiteral(view.Name)}';");
    }

    protected override DdlRetrieval GetRoutineDdlRetrieval(DbObjectRef routine, bool isFunction)
    {
        return new DdlRetrieval(
            "select 'create or replace ' || ltrim(listagg(text, '') within group (order by line)) as Definition" + Environment.NewLine +
            "from user_source" + Environment.NewLine +
            $"where name = '{EscapeSqlLiteral(routine.Name)}'" + Environment.NewLine +
            "  and type in ('PROCEDURE', 'FUNCTION');");
    }


    // Names are compared exactly as stored, so callers resolve names typed in SQL text first
    // (see ISqlDialect.ResolveQualifiedName). Without a schema, tables are looked up among the
    // connection user's own objects, as the tree lists them.
    private const string TableOwner = "coalesce(:SchemaName, user)";

    public override string GetColumnDefaultsStatement()
    {
        // Note: data_default is a LONG column; Oracle's managed provider reads it as the
        // first/only LONG-typed column in the result set, which is sufficient for typical
        // default expressions but may truncate unusually large ones (accepted limitation).
        return $"""
                select
                    column_name as "ColumnName",
                    data_default as "DefaultValueExpression"
                from all_tab_columns
                where owner = {TableOwner}
                  and table_name = :TableName
                order by column_id
                """;
    }

    public override string GetPrimaryKeyStatement()
    {
        return $"""
                select
                    c.constraint_name as "ConstraintName",
                    cc.column_name as "ColumnName",
                    cc.position as "OrdinalPosition"
                from all_constraints c
                join all_cons_columns cc on cc.owner = c.owner and cc.constraint_name = c.constraint_name
                where c.owner = {TableOwner}
                  and c.table_name = :TableName
                  and c.constraint_type = 'P'
                order by cc.position
                """;
    }

    public override string GetForeignKeysStatement()
    {
        return $"""
                select
                    c.constraint_name as "ConstraintName",
                    cc.column_name as "ColumnName",
                    cc.position as "OrdinalPosition",
                    rc.owner as "ReferencedSchemaName",
                    rc.table_name as "ReferencedTableName",
                    rcc.column_name as "ReferencedColumnName",
                    case c.delete_rule
                        when 'CASCADE' then 'cascade'
                        when 'SET NULL' then 'set null'
                        else '' end as "OnDeleteAction",
                    '' as "OnUpdateAction"
                from all_constraints c
                join all_cons_columns cc on cc.owner = c.owner and cc.constraint_name = c.constraint_name
                join all_constraints rc on rc.owner = c.r_owner and rc.constraint_name = c.r_constraint_name
                join all_cons_columns rcc on rcc.owner = rc.owner and rcc.constraint_name = rc.constraint_name and rcc.position = cc.position
                where c.owner = {TableOwner}
                  and c.table_name = :TableName
                  and c.constraint_type = 'R'
                order by c.constraint_name, cc.position
                """;
    }

    public override string GetIndexesStatement()
    {
        return $"""
                select
                    i.index_name as "IndexName",
                    case when i.uniqueness = 'UNIQUE' then 1 else 0 end as "IsUnique",
                    ic.column_name as "ColumnName",
                    case when ic.descend = 'DESC' then 1 else 0 end as "IsDescending",
                    ic.column_position as "OrdinalPosition"
                from all_indexes i
                join all_ind_columns ic on ic.index_owner = i.owner and ic.index_name = i.index_name
                where i.table_owner = {TableOwner}
                  and i.table_name = :TableName
                  and not exists (
                      select 1 from all_constraints c
                      where c.owner = i.table_owner
                        and c.table_name = i.table_name
                        and c.constraint_type = 'P'
                        and c.index_name = i.index_name
                  )
                order by i.index_name, ic.column_position
                """;
    }


    public override string GetObjectListStatement(DbObjectKind kind)
    {
        return kind switch
        {
            DbObjectKind.Table => """
                                  select user as "SchemaName", table_name as "Name", 1 as "IsDefaultSchema"
                                  from user_tables
                                  """,
            DbObjectKind.View => """
                                 select user as "SchemaName", view_name as "Name", 1 as "IsDefaultSchema"
                                 from user_views
                                 """,
            DbObjectKind.Procedure or DbObjectKind.Function => $"""
                select
                    user as "SchemaName",
                    object_name as "Name",
                    1 as "IsDefaultSchema",
                    object_name as "SpecificName",
                    null as "DataType"
                from user_objects
                where object_type = '{(kind == DbObjectKind.Procedure ? "PROCEDURE" : "FUNCTION")}'
                """,
            DbObjectKind.Sequence => """
                                     select
                                         user as "SchemaName",
                                         sequence_name as "Name",
                                         1 as "IsDefaultSchema",
                                         'increment ' || increment_by || ', last number ' || last_number as "Details"
                                     from user_sequences
                                     """,
            // unistr('\2192') is "→", kept out of the literal to avoid character set issues.
            DbObjectKind.Synonym => """
                                    select
                                        user as "SchemaName",
                                        synonym_name as "Name",
                                        1 as "IsDefaultSchema",
                                        unistr('\2192') || ' ' || table_owner || '.' || table_name
                                            || case when db_link is not null then '@' || db_link end as "Details"
                                    from user_synonyms
                                    """,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    public override string GetColumnsStatement()
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
        sb.AppendLine("    case when pk.column_name is not null then 1 else 0 end as IsPrimaryKey,");
        sb.AppendLine("    case when c.identity_column = 'YES' then 1 else 0 end as IsAutoGenerated,");
        sb.AppendLine("    case when c.data_default is not null then 1 else 0 end as HasDefaultValue");
        sb.AppendLine("from all_tab_columns c");
        sb.AppendLine("left join (");
        sb.AppendLine("    select ucc.owner, ucc.table_name, ucc.column_name");
        sb.AppendLine("    from all_constraints uc");
        sb.AppendLine("    join all_cons_columns ucc on ucc.owner = uc.owner and ucc.constraint_name = uc.constraint_name");
        sb.AppendLine($"    where uc.constraint_type = 'P' and uc.owner = {TableOwner} and uc.table_name = :TableName");
        sb.AppendLine(") pk on pk.owner = c.owner and pk.table_name = c.table_name and pk.column_name = c.column_name");
        sb.AppendLine($"where c.owner = {TableOwner} and c.table_name = :TableName");
        sb.AppendLine("order by c.column_id");

        return sb.ToString();
    }


    public override string GetRoutineParametersStatement()
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
               where object_name = :SpecificName
                 and package_name is null
                 and argument_name is not null
                 and data_level = 0
               order by sequence
               """;
    }
    public override string GetUniqueConstraintsStatement()
    {
        return $"""
                select
                    c.constraint_name as "ConstraintName",
                    cc.column_name as "ColumnName",
                    cc.position as "OrdinalPosition"
                from all_constraints c
                join all_cons_columns cc on cc.owner = c.owner and cc.constraint_name = c.constraint_name
                where c.owner = {TableOwner}
                  and c.table_name = :TableName
                  and c.constraint_type = 'U'
                order by c.constraint_name, cc.position
                """;
    }

    // Oracle stores NOT NULL as system-named check constraints; those are column nullability, not checks.
    public override CheckConstraintsQuery? GetCheckConstraintsQuery(string serverVersion)
    {
        return new CheckConstraintsQuery($"""
            select
                c.constraint_name as "ConstraintName",
                c.search_condition_vc as "Definition"
            from all_constraints c
            where c.owner = {TableOwner}
              and c.table_name = :TableName
              and c.constraint_type = 'C'
              and not (c.generated = 'GENERATED NAME' and c.search_condition_vc like '"%" IS NOT NULL')
            order by c.constraint_name
            """);
    }

    protected override DdlRetrieval GetTriggerDdlRetrieval(DbObjectRef trigger)
    {
        var owner = trigger.Schema is null
            ? "sys_context('USERENV', 'CURRENT_SCHEMA')"
            : $"'{EscapeSqlLiteral(trigger.Schema)}'";

        return new DdlRetrieval(
            $"select dbms_metadata.get_ddl('TRIGGER', '{EscapeSqlLiteral(trigger.Name)}', {owner}) as Definition from dual;",
            MetadataSessionSetup);
    }

    public override TriggersQuery GetTriggersQuery()
    {
        return new TriggersQuery($"""
            select
                owner as "SchemaName",
                trigger_name as "Name",
                lower(trigger_type) as "Timing",
                lower(replace(triggering_event, ' OR ', ', ')) as "Events"
            from all_triggers
            where table_owner = {TableOwner}
              and table_name = :TableName
              and base_object_type = 'TABLE'
            order by trigger_name
            """);
    }

    public override IReadOnlyList<DbObjectKind> RootObjectKinds { get; } =
        [DbObjectKind.Table, DbObjectKind.View, DbObjectKind.Procedure, DbObjectKind.Function, DbObjectKind.Sequence, DbObjectKind.Synonym];

    protected override DdlRetrieval GetSequenceDdlRetrieval(DbObjectRef sequence)
    {
        var owner = sequence.Schema is null
            ? "sys_context('USERENV', 'CURRENT_SCHEMA')"
            : $"'{EscapeSqlLiteral(sequence.Schema)}'";

        return new DdlRetrieval(
            $"select dbms_metadata.get_ddl('SEQUENCE', '{EscapeSqlLiteral(sequence.Name)}', {owner}) as Definition from dual;",
            MetadataSessionSetup);
    }

    protected override DdlRetrieval GetSynonymDdlRetrieval(DbObjectRef synonym)
    {
        var owner = synonym.Schema is null
            ? "sys_context('USERENV', 'CURRENT_SCHEMA')"
            : $"'{EscapeSqlLiteral(synonym.Schema)}'";

        return new DdlRetrieval(
            $"select dbms_metadata.get_ddl('SYNONYM', '{EscapeSqlLiteral(synonym.Name)}', {owner}) as Definition from dual;",
            MetadataSessionSetup);
    }


    private static string BeautifyTableDdl(DbObjectRef table, string ddl)
    {
        var normalized = ddl.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        var schemaName = table.Schema?.ToLowerInvariant();

        normalized = Regex.Replace(normalized, "\"([A-Z0-9_]+)\"", "$1");
        normalized = Regex.Replace(normalized, @"\s+MINVALUE\s+\S+\s+MAXVALUE\s+\S+\s+INCREMENT BY\s+\S+\s+START WITH\s+\S+\s+CACHE\s+\S+\s+NOORDER\s+NOCYCLE\s+NOKEEP\s+NOSCALE", string.Empty, RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"\s+ENABLE\b", string.Empty, RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"TIMESTAMP\s*\(\s*6\s*\)", "TIMESTAMP", RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"\s+USING INDEX\b", string.Empty, RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"\s+", " ");
        normalized = Regex.Replace(normalized, @"\s+,", ",");
        normalized = normalized.ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(schemaName))
            normalized = normalized.Replace($"{schemaName}.", string.Empty, StringComparison.Ordinal);

        normalized = Regex.Replace(normalized, @"create table\s+([a-z0-9_]+)\s+\(", "create table $1\n(", RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @",(?=\s*(?:[a-z_]|primary key|constraint))", ",\n    ", RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"\b(create table [a-z0-9_]+)\s*\(", "$1\n(", RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"\b(primary key|foreign key|references)\s*\(", "$1 (", RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"\s*\)\s*;", "\n);", RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"\n\s*primary key", "\n    primary key", RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"\n\s*constraint", "\n    constraint", RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"\n{2,}", "\n", RegexOptions.IgnoreCase);

        var lines = normalized
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line =>
            {
                if (line == "(" || line == ");" || line.StartsWith("create table ", StringComparison.Ordinal))
                    return line;

                return $"    {line}";
            });

        var result = string.Join(Environment.NewLine, lines).Trim();
        result = Regex.Replace(result, @"^create table\s+[a-z0-9_]+\.([a-z0-9_]+)", "create table $1", RegexOptions.IgnoreCase);

        if (!string.IsNullOrWhiteSpace(schemaName))
            result = result.Replace($"{schemaName}.", string.Empty, StringComparison.Ordinal);

        return result;
    }
}
