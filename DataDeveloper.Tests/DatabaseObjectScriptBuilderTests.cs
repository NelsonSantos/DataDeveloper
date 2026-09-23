using System;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Providers.Oracle;
using DataDeveloper.Data.Providers.MySql;
using DataDeveloper.Data.Providers.PostgresSql;
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

        Assert.Contains("select *", script, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("from [Orders];", script, System.StringComparison.Ordinal);
        Assert.DoesNotContain("top 100", script, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSelectRowsScript_UsesMySqlLimitSyntax()
    {
        var node = CreateNode(NodeType.Table, "orders");
        var settings = new MySqlConnectionSettings { DatabaseType = DatabaseType.MySql };

        var script = DatabaseObjectScriptBuilder.BuildSelectRowsScript(settings, node);

        Assert.Contains("select *", script, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("from `orders`;", script, System.StringComparison.Ordinal);
        Assert.DoesNotContain("limit 100", script, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSelectRowsScript_UsesPostgresLimitSyntax()
    {
        var node = CreateNode(NodeType.Table, "public.orders");
        var settings = new PostgresConnectionSettings { DatabaseType = DatabaseType.PostgresSql };

        var script = DatabaseObjectScriptBuilder.BuildSelectRowsScript(settings, node);

        Assert.Contains("select *", script, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("from \"public\".\"orders\";", script, System.StringComparison.Ordinal);
        Assert.DoesNotContain("limit 100", script, System.StringComparison.OrdinalIgnoreCase);
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
        var oracleScript = DatabaseObjectScriptBuilder.BuildExecuteProcedureScript(
            new OracleConnectionSettings { DatabaseType = DatabaseType.Oracle },
            procedureNode);
        var postgresScript = DatabaseObjectScriptBuilder.BuildExecuteProcedureScript(
            new PostgresConnectionSettings { DatabaseType = DatabaseType.PostgresSql },
            procedureNode);

        Assert.Equal("exec [ProcessOrders] @id = @id, @result = @result output;", sqlServerScript);
        Assert.Equal("call `ProcessOrders`(@id, @result);", mySqlScript);
        Assert.Equal("begin \"ProcessOrders\"(@id, @result); end;", oracleScript);
        Assert.Equal("call \"ProcessOrders\"(@id, @result);", postgresScript);
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
    public void BuildInsertScript_UsesLoadedColumns()
    {
        var tableNode = CreateNode(NodeType.Table, "Orders");
        var columnsFolder = CreateNode(NodeType.Columns, "Columns", isFolder: true, parent: tableNode);
        tableNode.Children.Add(columnsFolder);
        columnsFolder.Children.Add(CreateNode(NodeType.Column, "OrderId", parent: columnsFolder, tag: new ColumnModel
        {
            Name = "OrderId",
            DataType = "int",
            IsPrimaryKey = true
        }));
        columnsFolder.Children.Add(CreateNode(NodeType.Column, "CustomerName", parent: columnsFolder, tag: new ColumnModel
        {
            Name = "CustomerName",
            DataType = "varchar",
            Length = 50
        }));

        var script = DatabaseObjectScriptBuilder.BuildInsertScript(
            new SqlServerConnectionSettings { DatabaseType = DatabaseType.SqlServer },
            tableNode);

        Assert.Contains("insert into [Orders]", script, StringComparison.Ordinal);
        Assert.Contains("([OrderId], [CustomerName])", script, StringComparison.Ordinal);
        Assert.Contains("[OrderId]", script, StringComparison.Ordinal);
        Assert.Contains("[CustomerName]", script, StringComparison.Ordinal);
        Assert.Contains("@OrderId", script, StringComparison.Ordinal);
        Assert.Contains("@CustomerName", script, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildUpdateScript_PrefersPrimaryKeyInWhereClause()
    {
        var tableNode = CreateNode(NodeType.Table, "Orders");
        var columnsFolder = CreateNode(NodeType.Columns, "Columns", isFolder: true, parent: tableNode);
        tableNode.Children.Add(columnsFolder);
        columnsFolder.Children.Add(CreateNode(NodeType.Column, "OrderId", parent: columnsFolder, tag: new ColumnModel
        {
            Name = "OrderId",
            DataType = "int",
            IsPrimaryKey = true
        }));
        columnsFolder.Children.Add(CreateNode(NodeType.Column, "CustomerName", parent: columnsFolder, tag: new ColumnModel
        {
            Name = "CustomerName",
            DataType = "varchar",
            Length = 50
        }));

        var script = DatabaseObjectScriptBuilder.BuildUpdateScript(
            new SqlServerConnectionSettings { DatabaseType = DatabaseType.SqlServer },
            tableNode);

        Assert.Contains("set", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[CustomerName] = @CustomerName", script, StringComparison.Ordinal);
        Assert.Contains("where", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[OrderId] = @OrderId", script, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildDeleteScript_PrefersPrimaryKeyInWhereClause()
    {
        var tableNode = CreateNode(NodeType.Table, "Orders");
        var columnsFolder = CreateNode(NodeType.Columns, "Columns", isFolder: true, parent: tableNode);
        tableNode.Children.Add(columnsFolder);
        columnsFolder.Children.Add(CreateNode(NodeType.Column, "OrderId", parent: columnsFolder, tag: new ColumnModel
        {
            Name = "OrderId",
            DataType = "int",
            IsPrimaryKey = true
        }));

        var script = DatabaseObjectScriptBuilder.BuildDeleteScript(
            new SqlServerConnectionSettings { DatabaseType = DatabaseType.SqlServer },
            tableNode);

        Assert.Contains("delete from [Orders]", script, StringComparison.Ordinal);
        Assert.Contains("[OrderId] = @OrderId", script, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildDropScript_ReturnsDropTableStatement()
    {
        var node = CreateNode(NodeType.Table, "dbo.Customers");

        var script = DatabaseObjectScriptBuilder.BuildDropScript(
            new SqlServerConnectionSettings { DatabaseType = DatabaseType.SqlServer },
            node);

        Assert.Equal("drop table [dbo].[Customers];", script);
    }

    [Fact]
    public void BuildDropScript_ReturnsDropViewStatement()
    {
        var node = CreateNode(NodeType.View, "reporting.ActiveCustomers");

        var script = DatabaseObjectScriptBuilder.BuildDropScript(
            new MySqlConnectionSettings { DatabaseType = DatabaseType.MySql },
            node);

        Assert.Equal("drop view `reporting`.`ActiveCustomers`;", script);
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

    [Fact]
    public void BuildSelectFunctionScript_UsesOracleDualSyntax()
    {
        var functionNode = CreateNode(NodeType.Function, "GetTax");
        var parametersFolder = CreateNode(NodeType.Parameters, "Parameters", isFolder: true, parent: functionNode);
        functionNode.Children.Add(parametersFolder);
        parametersFolder.Children.Add(CreateNode(NodeType.Parameter, ":amount", parent: parametersFolder, tag: new RoutineParameterModel
        {
            Name = ":amount",
            Position = 1
        }));

        var script = DatabaseObjectScriptBuilder.BuildSelectFunctionScript(
            new OracleConnectionSettings { DatabaseType = DatabaseType.Oracle },
            functionNode);

        Assert.Equal("select \"GetTax\"(:amount) from dual;", script);
    }

    [Fact]
    public void BuildQualifiedName_QuotesPostgresIdentifiers()
    {
        var tableNode = CreateNode(NodeType.Table, "public.Order Details");

        var qualifiedName = DatabaseObjectScriptBuilder.BuildQualifiedName(
            new PostgresConnectionSettings { DatabaseType = DatabaseType.PostgresSql },
            tableNode);

        Assert.Equal("\"public\".\"Order Details\"", qualifiedName);
    }

    [Fact]
    public void BuildInsertScript_UsesOracleParameterPrefix()
    {
        var tableNode = CreateNode(NodeType.Table, "Orders");
        var columnsFolder = CreateNode(NodeType.Columns, "Columns", isFolder: true, parent: tableNode);
        tableNode.Children.Add(columnsFolder);
        columnsFolder.Children.Add(CreateNode(NodeType.Column, "OrderId", parent: columnsFolder, tag: new ColumnModel
        {
            Name = "OrderId",
            DataType = "number",
            IsPrimaryKey = true
        }));

        var script = DatabaseObjectScriptBuilder.BuildInsertScript(
            new OracleConnectionSettings { DatabaseType = DatabaseType.Oracle },
            tableNode);

        Assert.Contains(":OrderId", script, StringComparison.Ordinal);
        Assert.DoesNotContain("@OrderId", script, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(DatabaseType.SqlServer, "select next value for [sales].[order_number] as NextValue;")]
    [InlineData(DatabaseType.Oracle, "select \"sales\".\"order_number\".nextval as NextValue from dual;")]
    [InlineData(DatabaseType.PostgresSql, "select nextval('\"sales\".\"order_number\"') as NextValue;")]
    public void BuildSelectNextValueScript_UsesProviderSyntax(DatabaseType databaseType, string expected)
    {
        var node = CreateNode(NodeType.Sequence, "sales.order_number");
        IConnectionSettings settings = databaseType switch
        {
            DatabaseType.SqlServer => new SqlServerConnectionSettings { DatabaseType = databaseType },
            DatabaseType.Oracle => new OracleConnectionSettings { DatabaseType = databaseType },
            _ => new PostgresConnectionSettings { DatabaseType = databaseType }
        };

        Assert.Equal(expected, DatabaseObjectScriptBuilder.BuildSelectNextValueScript(settings, node));
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
