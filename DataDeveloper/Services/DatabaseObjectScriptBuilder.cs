using System;
using System.Collections.Generic;
using System.Linq;
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
        return connectionSettings.DatabaseType switch
        {
            DatabaseType.SqlServer => $"select top 100 *{Environment.NewLine}from {qualifiedName};",
            DatabaseType.MySql => $"select *{Environment.NewLine}from {qualifiedName}{Environment.NewLine}limit 100;",
            _ => $"select *{Environment.NewLine}from {qualifiedName};"
        };
    }

    public static string BuildCountRowsScript(IConnectionSettings connectionSettings, SchemaNode node)
    {
        var qualifiedName = BuildQualifiedName(connectionSettings, node);
        return $"select count(*) as TotalRows{Environment.NewLine}from {qualifiedName};";
    }

    public static string BuildExecuteProcedureScript(IConnectionSettings connectionSettings, SchemaNode node)
    {
        var qualifiedName = BuildQualifiedName(connectionSettings, node);
        var parameters = GetRoutineParameters(node);
        return connectionSettings.DatabaseType switch
        {
            DatabaseType.SqlServer => BuildSqlServerProcedureScript(qualifiedName, parameters),
            DatabaseType.MySql => BuildMySqlProcedureScript(qualifiedName, parameters),
            _ => qualifiedName
        };
    }

    public static string BuildSelectFunctionScript(IConnectionSettings connectionSettings, SchemaNode node)
    {
        var qualifiedName = BuildQualifiedName(connectionSettings, node);
        var parameters = GetRoutineParameters(node);
        var argumentList = string.Join(", ", parameters.Select(parameter => parameter.Name));
        return $"select {qualifiedName}({argumentList});";
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
}
