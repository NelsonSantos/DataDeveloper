using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Models;

namespace DataDeveloper.Services;

public static class DatabaseObjectScriptBuilder
{
    public static string BuildQualifiedName(IConnectionSettings connectionSettings, SchemaNode node)
    {
        return node.NodeType switch
        {
            NodeType.Table or NodeType.View or NodeType.Procedure or NodeType.Function =>
                QuoteObjectName(connectionSettings, node.Name),
            NodeType.Column => BuildQualifiedColumnName(connectionSettings, node),
            NodeType.Parameter => node.Name,
            _ => node.Name
        };
    }

    public static string BuildSelectRowsScript(IConnectionSettings connectionSettings, SchemaNode node)
    {
        var qualifiedName = BuildQualifiedName(connectionSettings, node);
        return $"select *{Environment.NewLine}from {qualifiedName};";
    }

    public static string BuildCountRowsScript(IConnectionSettings connectionSettings, SchemaNode node)
    {
        var qualifiedName = BuildQualifiedName(connectionSettings, node);
        return $"select count(*) as TotalRows{Environment.NewLine}from {qualifiedName};";
    }

    public static string BuildInsertScript(IConnectionSettings connectionSettings, SchemaNode node)
    {
        var qualifiedName = BuildQualifiedName(connectionSettings, node);
        var columns = GetLoadedColumns(node);
        if (columns.Count == 0)
            return $"insert into {qualifiedName}{Environment.NewLine}values ();";

        var quotedColumns = columns.Select(column => QuoteSingleIdentifier(connectionSettings, column.Name)).ToList();
        var placeholders = columns.Select(column => FormatValuePlaceholder(connectionSettings, column.Name)).ToList();

        return $"insert into {qualifiedName}{Environment.NewLine}({string.Join(", ", quotedColumns)}){Environment.NewLine}values{Environment.NewLine}({Environment.NewLine}    {string.Join($",{Environment.NewLine}    ", placeholders)}{Environment.NewLine});";
    }

    public static string BuildUpdateScript(IConnectionSettings connectionSettings, SchemaNode node)
    {
        var qualifiedName = BuildQualifiedName(connectionSettings, node);
        var columns = GetLoadedColumns(node);
        if (columns.Count == 0)
            return $"update {qualifiedName}{Environment.NewLine}set{Environment.NewLine}    -- column = value{Environment.NewLine}where 1 = 0;";

        var keyColumns = columns.Where(column => column.IsPrimaryKey).ToList();
        var nonKeyColumns = columns.Where(column => !column.IsPrimaryKey).ToList();
        var setColumns = (nonKeyColumns.Count > 0 ? nonKeyColumns : columns).ToList();
        var whereColumns = (keyColumns.Count > 0 ? keyColumns : columns.Take(1)).ToList();

        var assignments = setColumns
            .Select(column => $"{QuoteSingleIdentifier(connectionSettings, column.Name)} = {FormatValuePlaceholder(connectionSettings, column.Name)}");
        var predicates = whereColumns
            .Select(column => $"{QuoteSingleIdentifier(connectionSettings, column.Name)} = {FormatValuePlaceholder(connectionSettings, column.Name)}");

        return $"update {qualifiedName}{Environment.NewLine}set{Environment.NewLine}    {string.Join($",{Environment.NewLine}    ", assignments)}{Environment.NewLine}where{Environment.NewLine}    {string.Join($"{Environment.NewLine}    and ", predicates)};";
    }

    public static string BuildDeleteScript(IConnectionSettings connectionSettings, SchemaNode node)
    {
        var qualifiedName = BuildQualifiedName(connectionSettings, node);
        var columns = GetLoadedColumns(node);
        var keyColumns = columns.Where(column => column.IsPrimaryKey).ToList();
        var whereColumns = (keyColumns.Count > 0 ? keyColumns : columns.Take(1)).ToList();

        if (whereColumns.Count == 0)
            return $"delete from {qualifiedName}{Environment.NewLine}where 1 = 0;";

        var predicates = whereColumns
            .Select(column => $"{QuoteSingleIdentifier(connectionSettings, column.Name)} = {FormatValuePlaceholder(connectionSettings, column.Name)}");

        return $"delete from {qualifiedName}{Environment.NewLine}where{Environment.NewLine}    {string.Join($"{Environment.NewLine}    and ", predicates)};";
    }

    public static string BuildDropScript(IConnectionSettings connectionSettings, SchemaNode node)
    {
        var qualifiedName = BuildQualifiedName(connectionSettings, node);
        var objectType = node.NodeType switch
        {
            NodeType.Table => "table",
            NodeType.View => "view",
            NodeType.Procedure => "procedure",
            NodeType.Function => "function",
            _ => null
        };

        return objectType is null
            ? qualifiedName
            : $"drop {objectType} {qualifiedName};";
    }

    public static string BuildDdlScript(IConnectionSettings connectionSettings, SchemaNode node)
    {
        return node.NodeType switch
            {
                NodeType.Table => BuildCreateTableScript(connectionSettings, node),
                _ => BuildQualifiedName(connectionSettings, node)
            };
    }

    public static string? TryBuildNativeDdlRetrievalScript(IConnectionSettings connectionSettings, SchemaNode node)
    {
        return node.NodeType switch
        {
            NodeType.Table => BuildTableDdlRetrievalQuery(connectionSettings, node),
            NodeType.View => BuildViewDdlRetrievalQuery(connectionSettings, node),
            NodeType.Procedure => BuildRoutineDdlRetrievalQuery(connectionSettings, node, isFunction: false),
            NodeType.Function => BuildRoutineDdlRetrievalQuery(connectionSettings, node, isFunction: true),
            _ => null
        };
    }

    public static string BuildObjectDdlRetrievalScript(IConnectionSettings connectionSettings, SchemaNode node)
    {
        return node.NodeType switch
        {
            NodeType.View => BuildViewDdlRetrievalQuery(connectionSettings, node),
            NodeType.Procedure => BuildRoutineDdlRetrievalQuery(connectionSettings, node, isFunction: false),
            NodeType.Function => BuildRoutineDdlRetrievalQuery(connectionSettings, node, isFunction: true),
            _ => BuildQualifiedName(connectionSettings, node)
        };
    }

    public static string PostProcessDdl(IConnectionSettings connectionSettings, SchemaNode node, string ddl)
    {
        if (string.IsNullOrWhiteSpace(ddl))
            return ddl;

        if (connectionSettings.DatabaseType == DatabaseType.Oracle && node.NodeType == NodeType.Table)
            return BeautifyOracleTableDdl(node, ddl);

        return ddl;
    }

    public static string BuildExecuteProcedureScript(IConnectionSettings connectionSettings, SchemaNode node)
    {
        var qualifiedName = BuildQualifiedName(connectionSettings, node);
        var parameters = GetRoutineParameters(node);
        return connectionSettings.DatabaseType switch
        {
            DatabaseType.SqlServer => BuildSqlServerProcedureScript(qualifiedName, parameters),
            DatabaseType.Oracle => BuildOracleProcedureScript(qualifiedName, parameters),
            DatabaseType.PostgresSql => BuildPostgresProcedureScript(qualifiedName, parameters),
            DatabaseType.MySql => BuildMySqlProcedureScript(qualifiedName, parameters),
            _ => qualifiedName
        };
    }

    public static string BuildSelectFunctionScript(IConnectionSettings connectionSettings, SchemaNode node)
    {
        var qualifiedName = BuildQualifiedName(connectionSettings, node);
        var parameters = GetRoutineParameters(node);
        var argumentList = string.Join(", ", parameters.Select(parameter => parameter.Name));
        return connectionSettings.DatabaseType switch
        {
            DatabaseType.Oracle => $"select {qualifiedName}({argumentList}) from dual;",
            _ => $"select {qualifiedName}({argumentList});"
        };
    }

    private static string BuildQualifiedColumnName(IConnectionSettings connectionSettings, SchemaNode node)
    {
        var owner = FindOwningObjectNode(node);
        if (owner is null)
            return QuoteSingleIdentifier(connectionSettings, node.Name);

        return $"{BuildQualifiedName(connectionSettings, owner)}.{QuoteSingleIdentifier(connectionSettings, node.Name)}";
    }

    private static SchemaNode? FindOwningObjectNode(SchemaNode node)
    {
        var current = node.Parent;
        while (current is not null)
        {
            if (current.NodeType is NodeType.Table or NodeType.View or NodeType.Procedure or NodeType.Function)
                return current;

            current = current.Parent;
        }

        return null;
    }

    private static string QuoteObjectName(IConnectionSettings connectionSettings, string objectName)
    {
        var segments = objectName
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(segment => QuoteSingleIdentifier(connectionSettings, segment));

        return string.Join(".", segments);
    }

    private static string QuoteSingleIdentifier(IConnectionSettings connectionSettings, string identifier)
    {
        return connectionSettings.DatabaseType switch
        {
            DatabaseType.SqlServer => $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]",
            DatabaseType.Oracle or DatabaseType.SqLite or DatabaseType.PostgresSql => $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"",
            DatabaseType.MySql => $"`{identifier.Replace("`", "``", StringComparison.Ordinal)}`",
            _ => identifier
        };
    }

    private static IReadOnlyList<RoutineParameterModel> GetRoutineParameters(SchemaNode node)
    {
        var parameterFolder = node.Children.FirstOrDefault(child => child.NodeType == NodeType.Parameters);
        if (parameterFolder is null)
            return [];

        return parameterFolder.Children
            .Select(child => child.Tag as RoutineParameterModel)
            .Where(parameter => parameter is not null)
            .Where(parameter => IsInvocationParameter(parameter!))
            .OrderBy(parameter => parameter!.Position)
            .Cast<RoutineParameterModel>()
            .ToList();
    }

    private static IReadOnlyList<ColumnModel> GetLoadedColumns(SchemaNode node)
    {
        var columnsFolder = node.Children.FirstOrDefault(child => child.NodeType == NodeType.Columns);
        if (columnsFolder is null)
            return [];

        return columnsFolder.Children
            .Select(child => child.Tag as ColumnModel)
            .Where(column => column is not null)
            .Cast<ColumnModel>()
            .ToList();
    }

    private static bool IsInvocationParameter(RoutineParameterModel parameter)
    {
        return !string.Equals(parameter.Name, "@RETURN_VALUE", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(parameter.Name, "return", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildSqlServerProcedureScript(string qualifiedName, IReadOnlyList<RoutineParameterModel> parameters)
    {
        if (parameters.Count == 0)
            return $"exec {qualifiedName};";

        var assignments = parameters.Select(parameter =>
        {
            var assignment = $"{parameter.Name} = {parameter.Name}";
            if (!string.IsNullOrWhiteSpace(parameter.Mode) &&
                parameter.Mode.Contains("OUT", StringComparison.OrdinalIgnoreCase))
            {
                assignment += " output";
            }

            return assignment;
        });

        return $"exec {qualifiedName} {string.Join(", ", assignments)};";
    }

    private static string BuildMySqlProcedureScript(string qualifiedName, IReadOnlyList<RoutineParameterModel> parameters)
    {
        var argumentList = string.Join(", ", parameters.Select(parameter => parameter.Name));
        return $"call {qualifiedName}({argumentList});";
    }

    private static string BuildOracleProcedureScript(string qualifiedName, IReadOnlyList<RoutineParameterModel> parameters)
    {
        var argumentList = string.Join(", ", parameters.Select(parameter => parameter.Name));
        return $"begin {qualifiedName}({argumentList}); end;";
    }

    private static string BuildPostgresProcedureScript(string qualifiedName, IReadOnlyList<RoutineParameterModel> parameters)
    {
        var argumentList = string.Join(", ", parameters.Select(parameter => parameter.Name));
        return $"call {qualifiedName}({argumentList});";
    }

    private static string BuildCreateTableScript(IConnectionSettings connectionSettings, SchemaNode node)
    {
        var qualifiedName = BuildQualifiedName(connectionSettings, node);
        var columns = GetLoadedColumns(node);
        if (columns.Count == 0)
            return $"create table {qualifiedName}{Environment.NewLine}({Environment.NewLine}    -- load columns to generate DDL{Environment.NewLine});";

        var lines = columns
            .Select(column => $"    {QuoteSingleIdentifier(connectionSettings, column.Name)} {BuildColumnType(column)} {(column.IsNullable ? "null" : "not null")}")
            .ToList();

        var primaryKeys = columns.Where(column => column.IsPrimaryKey).ToList();
        if (primaryKeys.Count > 0)
        {
            var pkColumns = string.Join(", ", primaryKeys.Select(column => QuoteSingleIdentifier(connectionSettings, column.Name)));
            lines.Add($"    primary key ({pkColumns})");
        }

        return $"create table {qualifiedName}{Environment.NewLine}({Environment.NewLine}{string.Join($",{Environment.NewLine}", lines)}{Environment.NewLine});";
    }

    private static string BuildColumnType(ColumnModel column)
    {
        var dataType = column.DataType;
        if (column.Length != 0 &&
            dataType is "varchar" or "nvarchar" or "char" or "nchar" or "varbinary" or "binary")
        {
            return $"{dataType}({(column.Length == -1 ? "max" : column.Length)})";
        }

        if (dataType.Contains("date", StringComparison.OrdinalIgnoreCase) ||
            dataType.Contains("time", StringComparison.OrdinalIgnoreCase))
        {
            return dataType;
        }

        if (column.Precision != 0)
        {
            return column.Scale != 0
                ? $"{dataType}({column.Precision}, {column.Scale})"
                : $"{dataType}({column.Precision})";
        }

        return dataType;
    }

    private static string BuildRoutineDdlRetrievalQuery(IConnectionSettings connectionSettings, SchemaNode node, bool isFunction)
    {
        var objectName = BuildQualifiedName(connectionSettings, node);
        var (schemaName, routineName) = SplitQualifiedObjectName(node.Name);

        return connectionSettings.DatabaseType switch
        {
            DatabaseType.SqlServer => $"select object_definition(object_id(N'{EscapeSqlLiteral(node.Name)}')) as Definition;",
            DatabaseType.MySql => $"show create {(isFunction ? "function" : "procedure")} {objectName};",
            DatabaseType.Oracle =>
                BuildOracleRoutineSourceQuery(schemaName, routineName),
            DatabaseType.PostgresSql => BuildPostgresRoutineDdlScript(schemaName, routineName),
            _ => objectName
        };
    }

    private static string? BuildTableDdlRetrievalQuery(IConnectionSettings connectionSettings, SchemaNode node)
    {
        var objectName = BuildQualifiedName(connectionSettings, node);
        var escapedObjectName = EscapeSqlLiteral(node.Name);
        var (schemaName, tableName) = SplitQualifiedObjectName(node.Name);
        return connectionSettings.DatabaseType switch
        {
            DatabaseType.MySql => $"show create table {objectName};",
            DatabaseType.Oracle =>
                $"select dbms_metadata.get_ddl('TABLE', '{EscapeSqlLiteral(tableName)}', {BuildOracleSchemaExpression(node.Name, schemaName)}) as Definition from dual;",
            DatabaseType.SqlServer => BuildSqlServerTableDdlRetrievalQuery(escapedObjectName),
            DatabaseType.PostgresSql => BuildPostgresTableDdlRetrievalQuery(schemaName, tableName),
            DatabaseType.SqLite =>
                "select sql as Definition" + Environment.NewLine +
                "from sqlite_master" + Environment.NewLine +
                $"where type = 'table' and name = '{EscapeSqlLiteral(tableName)}';",
            _ => null
        };
    }

    private static string BuildSqlServerTableDdlRetrievalQuery(string objectName)
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

    private static string BuildPostgresTableDdlRetrievalQuery(string schemaName, string tableName)
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

    private static string BuildViewDdlRetrievalQuery(IConnectionSettings connectionSettings, SchemaNode node)
    {
        var objectName = BuildQualifiedName(connectionSettings, node);
        var (schemaName, viewName) = SplitQualifiedObjectName(node.Name);

        return connectionSettings.DatabaseType switch
        {
            DatabaseType.SqlServer => $"select object_definition(object_id(N'{EscapeSqlLiteral(node.Name)}')) as Definition;",
            DatabaseType.MySql => $"show create view {objectName};",
            DatabaseType.Oracle =>
                BuildOracleViewSourceQuery(schemaName, viewName),
            DatabaseType.PostgresSql =>
                "select 'create or replace view ' || quote_ident(n.nspname) || '.' || quote_ident(c.relname) || ' as' || E'\\n' || pg_get_viewdef(c.oid, true) as Definition" + Environment.NewLine +
                "from pg_class c" + Environment.NewLine +
                "join pg_namespace n on n.oid = c.relnamespace" + Environment.NewLine +
                $"where n.nspname = '{EscapeSqlLiteral(schemaName)}'" + Environment.NewLine +
                $"  and c.relname = '{EscapeSqlLiteral(viewName)}'" + Environment.NewLine +
                "  and c.relkind = 'v';",
            DatabaseType.SqLite =>
                "select sql as Definition" + Environment.NewLine +
                "from sqlite_master" + Environment.NewLine +
                $"where type = 'view' and name = '{EscapeSqlLiteral(viewName)}';",
            _ => objectName
        };
    }

    private static string BuildPostgresRoutineDdlScript(string schemaName, string routineName)
    {
        return
            $"select pg_get_functiondef(p.oid){Environment.NewLine}" +
            "from pg_proc p" + Environment.NewLine +
            "join pg_namespace n on n.oid = p.pronamespace" + Environment.NewLine +
            $"where n.nspname = '{EscapeSqlLiteral(schemaName)}'" + Environment.NewLine +
            $"  and p.proname = '{EscapeSqlLiteral(routineName)}';";
    }

    private static string BuildOracleViewSourceQuery(string schemaName, string viewName)
    {
        var ownerPredicate = BuildOracleOwnerPredicate(schemaName, "view_name", viewName);
        return
            "select 'create or replace view ' || view_name || chr(10) || 'as' || chr(10) || text_vc as Definition" + Environment.NewLine +
            "from user_views" + Environment.NewLine +
            $"where {ownerPredicate};";
    }

    private static string BuildOracleRoutineSourceQuery(string schemaName, string routineName)
    {
        var ownerPredicate = BuildOracleOwnerPredicate(schemaName, "name", routineName);
        return
            "select 'create or replace ' || ltrim(listagg(text, '') within group (order by line)) as Definition" + Environment.NewLine +
            "from user_source" + Environment.NewLine +
            $"where {ownerPredicate}" + Environment.NewLine +
            "  and type in ('PROCEDURE', 'FUNCTION');";
    }

    private static (string SchemaName, string ObjectName) SplitQualifiedObjectName(string objectName)
    {
        var parts = objectName
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();

        return parts.Length switch
        {
            >= 2 => (NormalizeIdentifier(parts[^2]), NormalizeIdentifier(parts[^1])),
            _ => ("public", NormalizeIdentifier(objectName))
        };
    }

    private static string NormalizeIdentifier(string identifier)
    {
        return identifier.Trim().Trim('[', ']', '`', '"');
    }

    private static string EscapeSqlLiteral(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }

    private static string BuildOracleSchemaExpression(string objectName, string schemaName)
    {
        return objectName.Contains('.', StringComparison.Ordinal)
            ? $"'{EscapeSqlLiteral(schemaName)}'"
            : "sys_context('USERENV', 'CURRENT_SCHEMA')";
    }

    private static string BuildOracleOwnerPredicate(string schemaName, string objectColumnName, string objectName)
    {
        return $"{objectColumnName} = '{EscapeSqlLiteral(objectName)}'";
    }

    private static string FormatValuePlaceholder(IConnectionSettings connectionSettings, string columnName)
    {
        var prefix = connectionSettings.DatabaseType == DatabaseType.Oracle ? ":" : "@";
        return $"{prefix}{NormalizeIdentifier(columnName)}";
    }

    private static string BeautifyOracleTableDdl(SchemaNode node, string ddl)
    {
        var normalized = ddl.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        var schemaName = node.Name.Contains('.', StringComparison.Ordinal)
            ? NormalizeIdentifier(node.Name.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0]).ToLowerInvariant()
            : null;

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
