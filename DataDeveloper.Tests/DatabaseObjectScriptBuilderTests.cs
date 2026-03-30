using System;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Providers.MySql;
using DataDeveloper.Data.Providers.SqlServer;
using DataDeveloper.Services;
using Xunit;

namespace DataDeveloper.Tests;

public class DatabaseObjectScriptBuilderTests
{
    [Fact]
    public void BuildSelectRowsScript_UsesSqlServerTopSyntax()
    {
        var node = CreateNode(NodeType.Table, "Orders");
        var settings = new SqlServerConnectionSettings { DatabaseType = DatabaseType.SqlServer };

        var script = DatabaseObjectScriptBuilder.BuildSelectRowsScript(settings, node);

        Assert.Contains("select top 100 *", script, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("from [Orders];", script, System.StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSelectRowsScript_UsesMySqlLimitSyntax()
    {
        var node = CreateNode(NodeType.Table, "orders");
        var settings = new MySqlConnectionSettings { DatabaseType = DatabaseType.MySql };

        var script = DatabaseObjectScriptBuilder.BuildSelectRowsScript(settings, node);

        Assert.Contains("select *", script, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("from `orders`", script, System.StringComparison.Ordinal);
        Assert.Contains("limit 100;", script, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildExecuteProcedureScript_UsesProviderSpecificSyntax()
    {
        var procedureNode = CreateNode(NodeType.Procedure, "ProcessOrders");
        var parametersFolder = CreateNode(NodeType.Parameters, "Parameters", isFolder: true, parent: procedureNode);
        procedureNode.Children.Add(parametersFolder);
        parametersFolder.Children.Add(CreateNode(NodeType.Parameter, "@id", parent: parametersFolder, tag: new RoutineParameterModel
        {
            Name = "@id",
            Position = 1,
            Mode = "IN"
        }));
        parametersFolder.Children.Add(CreateNode(NodeType.Parameter, "@result", parent: parametersFolder, tag: new RoutineParameterModel
        {
            Name = "@result",
            Position = 2,
            Mode = "OUT"
        }));
        parametersFolder.Children.Add(CreateNode(NodeType.Parameter, "@RETURN_VALUE", parent: parametersFolder, tag: new RoutineParameterModel
        {
            Name = "@RETURN_VALUE",
            Position = 0
        }));

        var sqlServerScript = DatabaseObjectScriptBuilder.BuildExecuteProcedureScript(
            new SqlServerConnectionSettings { DatabaseType = DatabaseType.SqlServer },
            procedureNode);
        var mySqlScript = DatabaseObjectScriptBuilder.BuildExecuteProcedureScript(
            new MySqlConnectionSettings { DatabaseType = DatabaseType.MySql },
            procedureNode);

        Assert.Equal("exec [ProcessOrders] @id = @id, @result = @result output;", sqlServerScript);
        Assert.Equal("call `ProcessOrders`(@id, @result);", mySqlScript);
    }

    [Fact]
    public void BuildQualifiedName_FormatsColumnWithOwner()
    {
        var tableNode = CreateNode(NodeType.Table, "Orders");
        var columnsFolder = CreateNode(NodeType.Columns, "Columns", isFolder: true, parent: tableNode);
        var columnNode = CreateNode(NodeType.Column, "OrderId", parent: columnsFolder);

        var qualifiedName = DatabaseObjectScriptBuilder.BuildQualifiedName(
            new SqlServerConnectionSettings { DatabaseType = DatabaseType.SqlServer },
            columnNode);

        Assert.Equal("[Orders].[OrderId]", qualifiedName);
    }

    [Fact]
    public void BuildSelectFunctionScript_UsesLoadedParameters()
    {
        var functionNode = CreateNode(NodeType.Function, "dbo.CalculateTax");
        var parametersFolder = CreateNode(NodeType.Parameters, "Parameters", isFolder: true, parent: functionNode);
        functionNode.Children.Add(parametersFolder);
        parametersFolder.Children.Add(CreateNode(NodeType.Parameter, "@amount", parent: parametersFolder, tag: new RoutineParameterModel
        {
            Name = "@amount",
            Position = 1
        }));
        parametersFolder.Children.Add(CreateNode(NodeType.Parameter, "@region", parent: parametersFolder, tag: new RoutineParameterModel
        {
            Name = "@region",
            Position = 2
        }));

        var script = DatabaseObjectScriptBuilder.BuildSelectFunctionScript(
            new SqlServerConnectionSettings { DatabaseType = DatabaseType.SqlServer },
            functionNode);

        Assert.Equal("select [dbo].[CalculateTax](@amount, @region);", script);
    }

    private static SchemaNode CreateNode(NodeType nodeType, string name, bool isFolder = false, SchemaNode? parent = null, object? tag = null)
    {
        return (SchemaNode)Activator.CreateInstance(
                   typeof(SchemaNode),
                   System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                   binder: null,
                   args: [nodeType, name, isFolder, parent, false, null, tag],
                   culture: null)!;
    }
}
