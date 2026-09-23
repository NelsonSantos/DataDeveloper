using System.Text;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Services.Metadata;

namespace DataDeveloper.Data.Providers.SqlServer;

public sealed class SqlServerObjectCatalog : ObjectCatalog
{
    public SqlServerObjectCatalog()
        : base(DatabaseType.SqlServer)
    {
    }

    protected override DdlRetrieval GetTableDdlRetrieval(DbObjectRef table)
    {
        return new DdlRetrieval(BuildTableDdlQuery(EscapeSqlLiteral(table.QualifiedName)));
    }

    protected override DdlRetrieval GetViewDdlRetrieval(DbObjectRef view)
    {
        return new DdlRetrieval(BuildObjectDefinitionQuery(view));
    }

    protected override DdlRetrieval GetRoutineDdlRetrieval(DbObjectRef routine, bool isFunction)
    {
        return new DdlRetrieval(BuildObjectDefinitionQuery(routine));
    }

    private static string BuildObjectDefinitionQuery(DbObjectRef databaseObject)
    {
        return $"select object_definition(object_id(N'{EscapeSqlLiteral(databaseObject.QualifiedName)}')) as Definition;";
    }

    private static string BuildTableDdlQuery(string objectName)
    {
        return
            "declare @ObjectId int = object_id(N'" + objectName + "');" + Environment.NewLine +
            "if @ObjectId is null" + Environment.NewLine +
            "begin" + Environment.NewLine +
            "    select cast(null as nvarchar(max)) as Definition;" + Environment.NewLine +
            "    return;" + Environment.NewLine +
            "end;" + Environment.NewLine +
            Environment.NewLine +
            "select" + Environment.NewLine +
            "    'create table ' + quotename(s.name) + '.' + quotename(t.name) + char(13) + '(' + char(13) +" + Environment.NewLine +
            "    stuff((" + Environment.NewLine +
            "        select" + Environment.NewLine +
            "            ',' + char(13) + '    ' + quotename(c.name) + ' ' +" + Environment.NewLine +
            "            case" + Environment.NewLine +
            "                when cc.object_id is not null then 'as ' + cc.definition" + Environment.NewLine +
            "                when ty.name in ('varchar', 'char', 'varbinary', 'binary')" + Environment.NewLine +
            "                    then ty.name + '(' + case when c.max_length = -1 then 'max' else cast(c.max_length as varchar(10)) end + ')'" + Environment.NewLine +
            "                when ty.name in ('nvarchar', 'nchar')" + Environment.NewLine +
            "                    then ty.name + '(' + case when c.max_length = -1 then 'max' else cast(c.max_length / 2 as varchar(10)) end + ')'" + Environment.NewLine +
            "                when ty.name in ('decimal', 'numeric')" + Environment.NewLine +
            "                    then ty.name + '(' + cast(c.precision as varchar(10)) + ', ' + cast(c.scale as varchar(10)) + ')'" + Environment.NewLine +
            "                when ty.name in ('datetime2', 'datetimeoffset', 'time')" + Environment.NewLine +
            "                    then ty.name + '(' + cast(c.scale as varchar(10)) + ')'" + Environment.NewLine +
            "                else ty.name" + Environment.NewLine +
            "            end +" + Environment.NewLine +
            "            case" + Environment.NewLine +
            "                when cc.object_id is not null then ''" + Environment.NewLine +
            "                when ic.object_id is not null" + Environment.NewLine +
            "                    then ' identity(' + cast(convert(bigint, ic.seed_value) as varchar(30)) + ', ' + cast(convert(bigint, ic.increment_value) as varchar(30)) + ')'" + Environment.NewLine +
            "                else ''" + Environment.NewLine +
            "            end +" + Environment.NewLine +
            "            case" + Environment.NewLine +
            "                when cc.object_id is not null then ''" + Environment.NewLine +
            "                when c.is_rowguidcol = 1 then ' rowguidcol'" + Environment.NewLine +
            "                else ''" + Environment.NewLine +
            "            end +" + Environment.NewLine +
            "            case when c.is_nullable = 1 then ' null' else ' not null' end +" + Environment.NewLine +
            "            case" + Environment.NewLine +
            "                when cc.object_id is not null then ''" + Environment.NewLine +
            "                else coalesce(' default ' + dc.definition, '')" + Environment.NewLine +
            "            end" + Environment.NewLine +
            "        from sys.columns c" + Environment.NewLine +
            "        join sys.types ty on ty.user_type_id = c.user_type_id" + Environment.NewLine +
            "        left join sys.computed_columns cc on cc.object_id = c.object_id and cc.column_id = c.column_id" + Environment.NewLine +
            "        left join sys.identity_columns ic on ic.object_id = c.object_id and ic.column_id = c.column_id" + Environment.NewLine +
            "        left join sys.default_constraints dc on dc.parent_object_id = c.object_id and dc.parent_column_id = c.column_id" + Environment.NewLine +
            "        where c.object_id = t.object_id" + Environment.NewLine +
            "        order by c.column_id" + Environment.NewLine +
            "        for xml path(''), type).value('.', 'nvarchar(max)'), 1, 3, '    ') +" + Environment.NewLine +
            "    coalesce((" + Environment.NewLine +
            "        select char(13) + ',' + char(13) + '    constraint ' + quotename(kc.name) + ' primary key ' +" + Environment.NewLine +
            "               case when i.type = 1 then 'clustered' else 'nonclustered' end +" + Environment.NewLine +
            "               ' (' +" + Environment.NewLine +
            "               stuff((" + Environment.NewLine +
            "                    select ', ' + quotename(c.name)" + Environment.NewLine +
            "                    from sys.index_columns ic2" + Environment.NewLine +
            "                    join sys.columns c on c.object_id = ic2.object_id and c.column_id = ic2.column_id" + Environment.NewLine +
            "                    where ic2.object_id = i.object_id and ic2.index_id = i.index_id and ic2.key_ordinal > 0" + Environment.NewLine +
            "                    order by ic2.key_ordinal" + Environment.NewLine +
            "                    for xml path(''), type).value('.', 'nvarchar(max)'), 1, 2, '') +" + Environment.NewLine +
            "               ')'" + Environment.NewLine +
            "        from sys.key_constraints kc" + Environment.NewLine +
            "        join sys.indexes i on i.object_id = kc.parent_object_id and i.index_id = kc.unique_index_id" + Environment.NewLine +
            "        where kc.parent_object_id = t.object_id and kc.type = 'PK'" + Environment.NewLine +
            "    ), '') +" + Environment.NewLine +
            "    coalesce((" + Environment.NewLine +
            "        select" + Environment.NewLine +
            "            (" + Environment.NewLine +
            "                select char(13) + ',' + char(13) + '    constraint ' + quotename(kc.name) + ' unique ' +" + Environment.NewLine +
            "                       case when i.type = 1 then 'clustered' else 'nonclustered' end +" + Environment.NewLine +
            "                       ' (' +" + Environment.NewLine +
            "                       stuff((" + Environment.NewLine +
            "                            select ', ' + quotename(c.name)" + Environment.NewLine +
            "                            from sys.index_columns ic2" + Environment.NewLine +
            "                            join sys.columns c on c.object_id = ic2.object_id and c.column_id = ic2.column_id" + Environment.NewLine +
            "                            where ic2.object_id = i.object_id and ic2.index_id = i.index_id and ic2.key_ordinal > 0" + Environment.NewLine +
            "                            order by ic2.key_ordinal" + Environment.NewLine +
            "                            for xml path(''), type).value('.', 'nvarchar(max)'), 1, 2, '') +" + Environment.NewLine +
            "                       ')'" + Environment.NewLine +
            "                from sys.key_constraints kc" + Environment.NewLine +
            "                join sys.indexes i on i.object_id = kc.parent_object_id and i.index_id = kc.unique_index_id" + Environment.NewLine +
            "                where kc.parent_object_id = t.object_id and kc.type = 'UQ'" + Environment.NewLine +
            "                for xml path(''), type" + Environment.NewLine +
            "            ).value('.', 'nvarchar(max)')" + Environment.NewLine +
            "    ), '') +" + Environment.NewLine +
            "    coalesce((" + Environment.NewLine +
            "        select" + Environment.NewLine +
            "            (" + Environment.NewLine +
            "                select char(13) + ',' + char(13) + '    constraint ' + quotename(cc.name) + ' check ' + cc.definition" + Environment.NewLine +
            "                from sys.check_constraints cc" + Environment.NewLine +
            "                where cc.parent_object_id = t.object_id" + Environment.NewLine +
            "                for xml path(''), type" + Environment.NewLine +
            "            ).value('.', 'nvarchar(max)')" + Environment.NewLine +
            "    ), '') +" + Environment.NewLine +
            "    coalesce((" + Environment.NewLine +
            "        select" + Environment.NewLine +
            "            (" + Environment.NewLine +
            "                select char(13) + ',' + char(13) + '    constraint ' + quotename(fk.name) + ' foreign key (' +" + Environment.NewLine +
            "                       stuff((" + Environment.NewLine +
            "                            select ', ' + quotename(pc.name)" + Environment.NewLine +
            "                            from sys.foreign_key_columns fkc2" + Environment.NewLine +
            "                            join sys.columns pc on pc.object_id = fkc2.parent_object_id and pc.column_id = fkc2.parent_column_id" + Environment.NewLine +
            "                            where fkc2.constraint_object_id = fk.object_id" + Environment.NewLine +
            "                            order by fkc2.constraint_column_id" + Environment.NewLine +
            "                            for xml path(''), type).value('.', 'nvarchar(max)'), 1, 2, '') +" + Environment.NewLine +
            "                       ') references ' + quotename(rs.name) + '.' + quotename(rt.name) + ' (' +" + Environment.NewLine +
            "                       stuff((" + Environment.NewLine +
            "                            select ', ' + quotename(rc.name)" + Environment.NewLine +
            "                            from sys.foreign_key_columns fkc3" + Environment.NewLine +
            "                            join sys.columns rc on rc.object_id = fkc3.referenced_object_id and rc.column_id = fkc3.referenced_column_id" + Environment.NewLine +
            "                            where fkc3.constraint_object_id = fk.object_id" + Environment.NewLine +
            "                            order by fkc3.constraint_column_id" + Environment.NewLine +
            "                            for xml path(''), type).value('.', 'nvarchar(max)'), 1, 2, '') +" + Environment.NewLine +
            "                       ')' +" + Environment.NewLine +
            "                       case fk.delete_referential_action" + Environment.NewLine +
            "                            when 1 then ' on delete cascade'" + Environment.NewLine +
            "                            when 2 then ' on delete set null'" + Environment.NewLine +
            "                            when 3 then ' on delete set default'" + Environment.NewLine +
            "                            else ''" + Environment.NewLine +
            "                       end +" + Environment.NewLine +
            "                       case fk.update_referential_action" + Environment.NewLine +
            "                            when 1 then ' on update cascade'" + Environment.NewLine +
            "                            when 2 then ' on update set null'" + Environment.NewLine +
            "                            when 3 then ' on update set default'" + Environment.NewLine +
            "                            else ''" + Environment.NewLine +
            "                       end" + Environment.NewLine +
            "                from sys.foreign_keys fk" + Environment.NewLine +
            "                join sys.tables rt on rt.object_id = fk.referenced_object_id" + Environment.NewLine +
            "                join sys.schemas rs on rs.schema_id = rt.schema_id" + Environment.NewLine +
            "                where fk.parent_object_id = t.object_id" + Environment.NewLine +
            "                for xml path(''), type" + Environment.NewLine +
            "            ).value('.', 'nvarchar(max)')" + Environment.NewLine +
            "    ), '') +" + Environment.NewLine +
            "    char(13) + ');' as Definition" + Environment.NewLine +
            "from sys.tables t" + Environment.NewLine +
            "join sys.schemas s on s.schema_id = t.schema_id" + Environment.NewLine +
            "where t.object_id = @ObjectId;" + Environment.NewLine +
            Environment.NewLine +
            "select" + Environment.NewLine +
            "    'create ' +" + Environment.NewLine +
            "    case when i.is_unique = 1 then 'unique ' else '' end +" + Environment.NewLine +
            "    case when i.type = 1 then 'clustered ' when i.type = 2 then 'nonclustered ' else '' end +" + Environment.NewLine +
            "    'index ' + quotename(i.name) + ' on ' + quotename(s.name) + '.' + quotename(t.name) + ' (' +" + Environment.NewLine +
            "    stuff((" + Environment.NewLine +
            "        select ', ' + quotename(c.name) + case when ic.is_descending_key = 1 then ' desc' else ' asc' end" + Environment.NewLine +
            "        from sys.index_columns ic" + Environment.NewLine +
            "        join sys.columns c on c.object_id = ic.object_id and c.column_id = ic.column_id" + Environment.NewLine +
            "        where ic.object_id = i.object_id and ic.index_id = i.index_id and ic.key_ordinal > 0" + Environment.NewLine +
            "        order by ic.key_ordinal" + Environment.NewLine +
            "        for xml path(''), type).value('.', 'nvarchar(max)'), 1, 2, '') +" + Environment.NewLine +
            "    ')' +" + Environment.NewLine +
            "    case when exists (" + Environment.NewLine +
            "            select 1" + Environment.NewLine +
            "            from sys.index_columns ic" + Environment.NewLine +
            "            where ic.object_id = i.object_id and ic.index_id = i.index_id and ic.is_included_column = 1" + Environment.NewLine +
            "        ) then ' include (' +" + Environment.NewLine +
            "            stuff((" + Environment.NewLine +
            "                select ', ' + quotename(c.name)" + Environment.NewLine +
            "                from sys.index_columns ic" + Environment.NewLine +
            "                join sys.columns c on c.object_id = ic.object_id and c.column_id = ic.column_id" + Environment.NewLine +
            "                where ic.object_id = i.object_id and ic.index_id = i.index_id and ic.is_included_column = 1" + Environment.NewLine +
            "                order by ic.index_column_id" + Environment.NewLine +
            "                for xml path(''), type).value('.', 'nvarchar(max)'), 1, 2, '') +" + Environment.NewLine +
            "            ')' else '' end +" + Environment.NewLine +
            "    ';' as Definition" + Environment.NewLine +
            "from sys.indexes i" + Environment.NewLine +
            "join sys.tables t on t.object_id = i.object_id" + Environment.NewLine +
            "join sys.schemas s on s.schema_id = t.schema_id" + Environment.NewLine +
            "where i.object_id = @ObjectId" + Environment.NewLine +
            "  and i.is_hypothetical = 0" + Environment.NewLine +
            "  and i.type in (1, 2)" + Environment.NewLine +
            "  and i.is_primary_key = 0" + Environment.NewLine +
            "  and i.is_unique_constraint = 0;";
    }

    // Without a schema, OBJECT_ID resolves the name through the user's default schema, then dbo.
    private const string TableObjectId =
        "object_id(case when @SchemaName is null then quotename(@TableName) else quotename(@SchemaName) + '.' + quotename(@TableName) end)";

    public override string GetColumnDefaultsStatement()
    {
        return $"""
               select
                   c.name as ColumnName,
                   dc.definition as DefaultValueExpression,
                   dc.name as DefaultConstraintName
               from sys.columns c
               left join sys.default_constraints dc
                   on dc.parent_object_id = c.object_id and dc.parent_column_id = c.column_id
               where c.object_id = {TableObjectId}
               order by c.column_id;
               """;
    }

    public override string GetPrimaryKeyStatement()
    {
        return $"""
               select
                   kc.name as ConstraintName,
                   c.name as ColumnName,
                   ic.key_ordinal as OrdinalPosition
               from sys.key_constraints kc
               join sys.indexes i on i.object_id = kc.parent_object_id and i.index_id = kc.unique_index_id
               join sys.index_columns ic on ic.object_id = i.object_id and ic.index_id = i.index_id and ic.key_ordinal > 0
               join sys.columns c on c.object_id = ic.object_id and c.column_id = ic.column_id
               where kc.parent_object_id = {TableObjectId} and kc.type = 'PK'
               order by ic.key_ordinal;
               """;
    }

    public override string GetForeignKeysStatement()
    {
        return $"""
               select
                   fk.name as ConstraintName,
                   pc.name as ColumnName,
                   fkc.constraint_column_id as OrdinalPosition,
                   rs.name as ReferencedSchemaName,
                   rt.name as ReferencedTableName,
                   rc.name as ReferencedColumnName,
                   case fk.delete_referential_action
                       when 1 then 'cascade' when 2 then 'set null' when 3 then 'set default' else '' end as OnDeleteAction,
                   case fk.update_referential_action
                       when 1 then 'cascade' when 2 then 'set null' when 3 then 'set default' else '' end as OnUpdateAction
               from sys.foreign_keys fk
               join sys.foreign_key_columns fkc on fkc.constraint_object_id = fk.object_id
               join sys.columns pc on pc.object_id = fkc.parent_object_id and pc.column_id = fkc.parent_column_id
               join sys.columns rc on rc.object_id = fkc.referenced_object_id and rc.column_id = fkc.referenced_column_id
               join sys.tables rt on rt.object_id = fk.referenced_object_id
               join sys.schemas rs on rs.schema_id = rt.schema_id
               where fk.parent_object_id = {TableObjectId}
               order by fk.name, fkc.constraint_column_id;
               """;
    }

    public override string GetIndexesStatement()
    {
        return $"""
               select
                   i.name as IndexName,
                   i.is_unique as IsUnique,
                   c.name as ColumnName,
                   ic.is_descending_key as IsDescending,
                   ic.key_ordinal as OrdinalPosition,
                   cast(case when i.type = 1 then 1 else 0 end as bit) as IsClustered,
                   nullif(i.fill_factor, 0) as [FillFactor]
               from sys.indexes i
               join sys.index_columns ic on ic.object_id = i.object_id and ic.index_id = i.index_id and ic.key_ordinal > 0
               join sys.columns c on c.object_id = ic.object_id and c.column_id = ic.column_id
               where i.object_id = {TableObjectId}
                 and i.is_primary_key = 0
                 and i.is_unique_constraint = 0
                 and i.is_hypothetical = 0
                 and i.type in (1, 2)
               order by i.name, ic.key_ordinal;
               """;
    }

    public override string GetObjectListStatement(DbObjectKind kind)
    {
        return kind switch
        {
            DbObjectKind.Table => """
                                  select
                                      table_schema as SchemaName,
                                      table_name as Name,
                                      cast(case when table_schema = schema_name() then 1 else 0 end as bit) as IsDefaultSchema
                                  from information_schema.tables
                                  where table_type = 'BASE TABLE';
                                  """,
            DbObjectKind.View => """
                                 select
                                     table_schema as SchemaName,
                                     table_name as Name,
                                     cast(case when table_schema = schema_name() then 1 else 0 end as bit) as IsDefaultSchema
                                 from information_schema.views;
                                 """,
            DbObjectKind.Procedure or DbObjectKind.Function => $"""
                select
                    r.routine_schema as SchemaName,
                    r.routine_name as Name,
                    cast(case when r.routine_schema = schema_name() then 1 else 0 end as bit) as IsDefaultSchema,
                    concat(r.routine_schema, '.', r.routine_name) as SpecificName,
                    r.data_type as DataType
                from information_schema.routines r
                inner join sys.objects o
                    on o.object_id = object_id(quotename(r.routine_schema) + '.' + quotename(r.routine_name))
                where r.routine_type = '{(kind == DbObjectKind.Procedure ? "PROCEDURE" : "FUNCTION")}'
                  and r.routine_schema not in ('sys', 'INFORMATION_SCHEMA')
                  and o.is_ms_shipped = 0;
                """,
            DbObjectKind.Sequence => """
                                     select
                                         schema_name(s.schema_id) as SchemaName,
                                         s.name as Name,
                                         cast(case when schema_name(s.schema_id) = schema_name() then 1 else 0 end as bit) as IsDefaultSchema,
                                         concat('increment ', cast(s.increment as nvarchar(40)), ', current ', cast(s.current_value as nvarchar(40))) as Details
                                     from sys.sequences s;
                                     """,
            // nchar(8594) is "→", kept out of the literal to avoid code page issues.
            DbObjectKind.Synonym => """
                                    select
                                        schema_name(s.schema_id) as SchemaName,
                                        s.name as Name,
                                        cast(case when schema_name(s.schema_id) = schema_name() then 1 else 0 end as bit) as IsDefaultSchema,
                                        nchar(8594) + N' ' + s.base_object_name as Details
                                    from sys.synonyms s;
                                    """,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    public override string GetColumnsStatement()
    {
        var sb = new StringBuilder();

        sb.AppendLine("SELECT ");
        sb.AppendLine("    c.name AS [Name],");
        sb.AppendLine("    t.name AS DataType,");
        sb.AppendLine("    CASE ");
        sb.AppendLine("        WHEN t.name IN ('nvarchar', 'nchar') AND c.max_length > 0 ");
        sb.AppendLine("            THEN CAST(c.max_length / 2 AS VARCHAR)");
        sb.AppendLine("        WHEN t.name IN ('varchar', 'char', 'varbinary') AND c.max_length > 0 ");
        sb.AppendLine("            THEN CAST(c.max_length AS VARCHAR)");
        sb.AppendLine("        ELSE CAST(c.max_length AS VARCHAR)");
        sb.AppendLine("    END AS Length,");
        sb.AppendLine("    c.precision AS Precision,");
        sb.AppendLine("    c.scale AS Scale,");
        sb.AppendLine("    c.is_nullable as IsNullable,");
        sb.AppendLine("    CASE WHEN k.column_id IS NOT NULL THEN 1 ELSE 0 END AS IsPrimaryKey,");
        sb.AppendLine("    c.is_identity as IsAutoGenerated,");
        sb.AppendLine("    CASE WHEN c.default_object_id <> 0 THEN 1 ELSE 0 END AS HasDefaultValue");
        sb.AppendLine("FROM ");
        sb.AppendLine("    sys.columns c");
        sb.AppendLine("JOIN ");
        sb.AppendLine("    sys.types t ON c.user_type_id = t.user_type_id");
        sb.AppendLine("LEFT JOIN (");
        sb.AppendLine("    SELECT ic.object_id, ic.column_id");
        sb.AppendLine("    FROM sys.indexes i");
        sb.AppendLine("    INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id");
        sb.AppendLine("    WHERE i.is_primary_key = 1");
        sb.AppendLine(") k ON c.object_id = k.object_id AND c.column_id = k.column_id");
        sb.AppendLine("WHERE ");
        sb.AppendLine($"    c.object_id = {TableObjectId}");
        sb.AppendLine("ORDER BY ");
        sb.AppendLine("    c.column_id;");
        
        return sb.ToString();
    }


    public override string GetRoutineParametersStatement()
    {
        return """
               select
                   coalesce(p.parameter_name, '@RETURN_VALUE') as Name,
                   p.data_type as DataType,
                   coalesce(p.character_maximum_length, 0) as Length,
                   coalesce(p.numeric_precision, 0) as Precision,
                   coalesce(p.numeric_scale, 0) as Scale,
                   cast(0 as bit) as IsNullable,
                   p.parameter_mode as Mode,
                   p.ordinal_position as Position
               from information_schema.parameters p
               where concat(p.specific_schema, '.', p.specific_name) = @SpecificName
               order by p.ordinal_position;
               """;
    }

    public override string GetUniqueConstraintsStatement()
    {
        return $"""
                select
                    kc.name as ConstraintName,
                    c.name as ColumnName,
                    ic.key_ordinal as OrdinalPosition
                from sys.key_constraints kc
                join sys.index_columns ic on ic.object_id = kc.parent_object_id and ic.index_id = kc.unique_index_id and ic.key_ordinal > 0
                join sys.columns c on c.object_id = ic.object_id and c.column_id = ic.column_id
                where kc.parent_object_id = {TableObjectId} and kc.type = 'UQ'
                order by kc.name, ic.key_ordinal;
                """;
    }

    public override CheckConstraintsQuery? GetCheckConstraintsQuery(string serverVersion)
    {
        return new CheckConstraintsQuery($"""
            select
                cc.name as ConstraintName,
                cc.definition as Definition
            from sys.check_constraints cc
            where cc.parent_object_id = {TableObjectId}
            order by cc.name;
            """);
    }

    // A trigger belongs to its table's schema, so the schema-qualified name identifies it.
    protected override DdlRetrieval GetTriggerDdlRetrieval(DbObjectRef trigger)
    {
        return new DdlRetrieval(BuildObjectDefinitionQuery(trigger));
    }

    public override TriggersQuery GetTriggersQuery()
    {
        return new TriggersQuery($"""
            select
                object_schema_name(t.parent_id) as SchemaName,
                t.name as Name,
                case when t.is_instead_of_trigger = 1 then 'instead of' else 'after' end as Timing,
                stuff((
                    select ', ' + lower(te.type_desc)
                    from sys.trigger_events te
                    where te.object_id = t.object_id
                    order by te.type
                    for xml path('')), 1, 2, '') as Events
            from sys.triggers t
            where t.parent_id = {TableObjectId}
            order by t.name;
            """);
    }

    public override IReadOnlyList<DbObjectKind> RootObjectKinds { get; } =
        [DbObjectKind.Table, DbObjectKind.View, DbObjectKind.Procedure, DbObjectKind.Function, DbObjectKind.Sequence, DbObjectKind.Synonym];

    // SQL Server has no function returning a sequence's DDL, so it is assembled from sys.sequences.
    protected override DdlRetrieval GetSequenceDdlRetrieval(DbObjectRef sequence)
    {
        return new DdlRetrieval(
            "select" + Environment.NewLine +
            "    'create sequence ' + quotename(schema_name(s.schema_id)) + '.' + quotename(s.name) + char(13) + char(10) +" + Environment.NewLine +
            "    '    as ' + type_name(s.user_type_id) + char(13) + char(10) +" + Environment.NewLine +
            "    '    start with ' + cast(s.start_value as nvarchar(40)) + char(13) + char(10) +" + Environment.NewLine +
            "    '    increment by ' + cast(s.increment as nvarchar(40)) + char(13) + char(10) +" + Environment.NewLine +
            "    '    minvalue ' + cast(s.minimum_value as nvarchar(40)) + char(13) + char(10) +" + Environment.NewLine +
            "    '    maxvalue ' + cast(s.maximum_value as nvarchar(40)) + char(13) + char(10) +" + Environment.NewLine +
            "    '    ' + case when s.is_cycling = 1 then 'cycle' else 'no cycle' end + char(13) + char(10) +" + Environment.NewLine +
            "    '    ' + case when s.is_cached = 0 then 'no cache' when s.cache_size is null then 'cache' else 'cache ' + cast(s.cache_size as nvarchar(20)) end + ';' as Definition" + Environment.NewLine +
            "from sys.sequences s" + Environment.NewLine +
            $"where s.object_id = object_id(N'{EscapeSqlLiteral(sequence.QualifiedName)}');");
    }

    protected override DdlRetrieval GetSynonymDdlRetrieval(DbObjectRef synonym)
    {
        return new DdlRetrieval(
            "select 'create synonym ' + quotename(schema_name(s.schema_id)) + '.' + quotename(s.name) + ' for ' + s.base_object_name + ';' as Definition" + Environment.NewLine +
            "from sys.synonyms s" + Environment.NewLine +
            $"where s.object_id = object_id(N'{EscapeSqlLiteral(synonym.QualifiedName)}');");
    }
}
