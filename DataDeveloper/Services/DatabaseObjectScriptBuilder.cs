using System;
using System.Collections.Generic;
using System.Linq;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Services.SqlDialects;

namespace DataDeveloper.Services;

public static class DatabaseObjectScriptBuilder
{
    public static string BuildQualifiedName(IConnectionSettings connectionSettings, SchemaNode node)
    {
        return node.NodeType switch
        {
            NodeType.Table or NodeType.View or NodeType.Procedure or NodeType.Function or NodeType.Sequence or NodeType.Synonym =>
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

    public static string BuildSelectNextValueScript(IConnectionSettings connectionSettings, SchemaNode node)
    {
        var qualifiedName = BuildQualifiedName(connectionSettings, node);
        return connectionSettings.DatabaseType switch
        {
            DatabaseType.SqlServer => $"select next value for {qualifiedName} as NextValue;",
            DatabaseType.Oracle => $"select {qualifiedName}.nextval as NextValue from dual;",
            DatabaseType.PostgresSql => $"select nextval('{qualifiedName.Replace("'", "''", StringComparison.Ordinal)}') as NextValue;",
            _ => qualifiedName
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
        return SqlDialect.For(connectionSettings.DatabaseType).QuoteQualifiedName(objectName);
    }

    private static string QuoteSingleIdentifier(IConnectionSettings connectionSettings, string identifier)
    {
        return SqlDialect.For(connectionSettings.DatabaseType).QuoteIdentifier(identifier);
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

    private static string NormalizeIdentifier(string identifier)
    {
        return identifier.Trim().Trim('[', ']', '`', '"');
    }

    private static string FormatValuePlaceholder(IConnectionSettings connectionSettings, string columnName)
    {
        return SqlDialect.For(connectionSettings.DatabaseType).FormatParameterReference(NormalizeIdentifier(columnName));
    }
}
