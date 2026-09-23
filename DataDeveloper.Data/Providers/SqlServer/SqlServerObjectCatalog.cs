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
}
