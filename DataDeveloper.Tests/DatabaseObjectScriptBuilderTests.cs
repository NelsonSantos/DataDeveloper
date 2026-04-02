using System;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Providers.Oracle;
using DataDeveloper.Data.Providers.MySql;
using DataDeveloper.Data.Providers.PostgresSql;
using DataDeveloper.Data.Providers.SqLite;
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
    public void BuildDdlScript_ForTable_UsesLoadedColumns()
    {
        var tableNode = CreateNode(NodeType.Table, "dbo.Orders");
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
            Length = 50,
            IsNullable = true
        }));
        columnsFolder.Children.Add(CreateNode(NodeType.Column, "CreatedAt", parent: columnsFolder, tag: new ColumnModel
        {
            Name = "CreatedAt",
            DataType = "datetime",
            Precision = 7,
            IsNullable = false
        }));

        var script = DatabaseObjectScriptBuilder.BuildDdlScript(
            new SqlServerConnectionSettings { DatabaseType = DatabaseType.SqlServer },
            tableNode);

        Assert.Contains("create table [dbo].[Orders]", script, StringComparison.Ordinal);
        Assert.Contains("[OrderId] int not null", script, StringComparison.Ordinal);
        Assert.Contains("[CustomerName] varchar(50) null", script, StringComparison.Ordinal);
        Assert.Contains("[CreatedAt] datetime not null", script, StringComparison.Ordinal);
        Assert.DoesNotContain("datetime(7)", script, StringComparison.Ordinal);
        Assert.Contains("primary key ([OrderId])", script, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildObjectDdlRetrievalScript_ForMySqlProcedure_UsesShowCreate()
    {
        var procedureNode = CreateNode(NodeType.Procedure, "ProcessOrders");

        var script = DatabaseObjectScriptBuilder.BuildObjectDdlRetrievalScript(
            new MySqlConnectionSettings { DatabaseType = DatabaseType.MySql },
            procedureNode);

        Assert.Equal("show create procedure `ProcessOrders`;", script);
    }

    [Fact]
    public void TryBuildNativeDdlRetrievalScript_ForMySqlTable_UsesShowCreateTable()
    {
        var tableNode = CreateNode(NodeType.Table, "sales.OrderItems");

        var script = DatabaseObjectScriptBuilder.TryBuildNativeDdlRetrievalScript(
            new MySqlConnectionSettings { DatabaseType = DatabaseType.MySql },
            tableNode);

        Assert.Equal("show create table `sales`.`OrderItems`;", script);
    }

    [Fact]
    public void TryBuildNativeDdlRetrievalScript_ForSqlServerTable_ReturnsNull()
    {
        var tableNode = CreateNode(NodeType.Table, "dbo.Orders");

        var script = DatabaseObjectScriptBuilder.TryBuildNativeDdlRetrievalScript(
            new SqlServerConnectionSettings { DatabaseType = DatabaseType.SqlServer },
            tableNode);

        Assert.NotNull(script);
        Assert.Contains("declare @ObjectId int = object_id(N'dbo.Orders');", script, StringComparison.Ordinal);
        Assert.Contains("from sys.tables t", script, StringComparison.Ordinal);
        Assert.Contains("join sys.columns c", script, StringComparison.Ordinal);
        Assert.Contains("left join sys.identity_columns ic", script, StringComparison.Ordinal);
        Assert.Contains("left join sys.default_constraints dc", script, StringComparison.Ordinal);
        Assert.Contains("left join sys.computed_columns cc", script, StringComparison.Ordinal);
        Assert.Contains("from sys.key_constraints kc", script, StringComparison.Ordinal);
        Assert.Contains("from sys.check_constraints cc", script, StringComparison.Ordinal);
        Assert.Contains("from sys.foreign_keys fk", script, StringComparison.Ordinal);
        Assert.Contains("from sys.indexes i", script, StringComparison.Ordinal);
        Assert.Contains("as Definition", script, StringComparison.Ordinal);
    }

    [Fact]
    public void TryBuildNativeDdlRetrievalScript_ForPostgresTable_UsesCatalogFunctions()
    {
        var tableNode = CreateNode(NodeType.Table, "sales.order_items");

        var script = DatabaseObjectScriptBuilder.TryBuildNativeDdlRetrievalScript(
            new PostgresConnectionSettings { DatabaseType = DatabaseType.PostgresSql },
            tableNode);

        Assert.NotNull(script);
        Assert.Contains("from pg_class c", script, StringComparison.Ordinal);
        Assert.Contains("join pg_namespace n on n.oid = c.relnamespace", script, StringComparison.Ordinal);
        Assert.Contains("format_type(a.atttypid, a.atttypmod)", script, StringComparison.Ordinal);
        Assert.Contains("pg_get_expr(ad.adbin, ad.adrelid)", script, StringComparison.Ordinal);
        Assert.Contains("pg_get_constraintdef(con.oid, true)", script, StringComparison.Ordinal);
        Assert.Contains("pg_get_indexdef(i.indexrelid)", script, StringComparison.Ordinal);
        Assert.Contains("where n.nspname = 'sales'", script, StringComparison.Ordinal);
        Assert.Contains("and c.relname = 'order_items'", script, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildObjectDdlRetrievalScript_ForSqlServerFunction_UsesObjectDefinition()
    {
        var functionNode = CreateNode(NodeType.Function, "dbo.CalculateTax");

        var script = DatabaseObjectScriptBuilder.BuildObjectDdlRetrievalScript(
            new SqlServerConnectionSettings { DatabaseType = DatabaseType.SqlServer },
            functionNode);

        Assert.Equal("select object_definition(object_id(N'dbo.CalculateTax')) as Definition;", script);
    }

    [Fact]
    public void BuildObjectDdlRetrievalScript_ForMySqlView_UsesShowCreateView()
    {
        var viewNode = CreateNode(NodeType.View, "SalesSummary");

        var script = DatabaseObjectScriptBuilder.BuildObjectDdlRetrievalScript(
            new MySqlConnectionSettings { DatabaseType = DatabaseType.MySql },
            viewNode);

        Assert.Equal("show create view `SalesSummary`;", script);
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

    [Fact]
    public void TryBuildNativeDdlRetrievalScript_ForOracleTable_UsesDbmsMetadata()
    {
        var tableNode = CreateNode(NodeType.Table, "HR.Orders");

        var script = DatabaseObjectScriptBuilder.TryBuildNativeDdlRetrievalScript(
            new OracleConnectionSettings { DatabaseType = DatabaseType.Oracle },
            tableNode);

        Assert.Equal("select dbms_metadata.get_ddl('TABLE', 'Orders', 'HR') as Definition from dual;", script);
    }

    [Fact]
    public void TryBuildNativeDdlRetrievalScript_ForSqLiteTable_UsesSqliteMaster()
    {
        var tableNode = CreateNode(NodeType.Table, "orders");

        var script = DatabaseObjectScriptBuilder.TryBuildNativeDdlRetrievalScript(
            new SqLiteConnectionSettings { DatabaseType = DatabaseType.SqLite },
            tableNode);

        Assert.Contains("from sqlite_master", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("type = 'table'", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name = 'orders'", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildObjectDdlRetrievalScript_ForOracleFunction_UsesUserSource()
    {
        var functionNode = CreateNode(NodeType.Function, "HR.CalculateTax");

        var script = DatabaseObjectScriptBuilder.BuildObjectDdlRetrievalScript(
            new OracleConnectionSettings { DatabaseType = DatabaseType.Oracle },
            functionNode);

        Assert.Contains("from user_source", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("'create or replace ' || ltrim(listagg(text, '') within group (order by line))", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name = 'CalculateTax'", script, StringComparison.Ordinal);
        Assert.Contains("type in ('PROCEDURE', 'FUNCTION')", script, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildObjectDdlRetrievalScript_ForSqLiteView_UsesSqliteMaster()
    {
        var viewNode = CreateNode(NodeType.View, "active_orders");

        var script = DatabaseObjectScriptBuilder.BuildObjectDdlRetrievalScript(
            new SqLiteConnectionSettings { DatabaseType = DatabaseType.SqLite },
            viewNode);

        Assert.Contains("from sqlite_master", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("type = 'view'", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name = 'active_orders'", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildObjectDdlRetrievalScript_ForOracleView_UsesUserViews()
    {
        var viewNode = CreateNode(NodeType.View, "HR.OPEN_ORDERS");

        var script = DatabaseObjectScriptBuilder.BuildObjectDdlRetrievalScript(
            new OracleConnectionSettings { DatabaseType = DatabaseType.Oracle },
            viewNode);

        Assert.Contains("from user_views", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("chr(10)", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("text_vc", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("view_name = 'OPEN_ORDERS'", script, StringComparison.Ordinal);
        Assert.Contains("create or replace view", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PostProcessDdl_ForOracleTable_RemovesMetadataNoise()
    {
        var node = CreateNode(NodeType.Table, "DATADEVELOPER.ORDERS");
        var rawDdl = """
                       CREATE TABLE "DATADEVELOPER"."ORDERS" 
                          ( "ORDER_ID" NUMBER GENERATED BY DEFAULT ON NULL AS IDENTITY MINVALUE 1 MAXVALUE 9999999999999999999999999999 INCREMENT BY 1 START WITH 1 CACHE 20 NOORDER NOCYCLE NOKEEP NOSCALE NOT NULL ENABLE, 
                            "CUSTOMER_ID" NUMBER NOT NULL ENABLE, 
                            "ORDER_TOTAL" NUMBER(10,2) NOT NULL ENABLE, 
                            "STATUS" VARCHAR2(30) DEFAULT 'OPEN' NOT NULL ENABLE, 
                            "CREATED_AT" TIMESTAMP (6) DEFAULT current_timestamp NOT NULL ENABLE, 
                             PRIMARY KEY ("ORDER_ID")
                           USING INDEX ENABLE, 
                             CONSTRAINT "FK_ORDERS_CUSTOMERS" FOREIGN KEY ("CUSTOMER_ID")
                              REFERENCES "DATADEVELOPER"."CUSTOMERS" ("CUSTOMER_ID") ENABLE
                          ) ;
                       """;

        var ddl = DatabaseObjectScriptBuilder.PostProcessDdl(
            new OracleConnectionSettings { DatabaseType = DatabaseType.Oracle },
            node,
            rawDdl);

        Assert.DoesNotContain("MINVALUE", ddl, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ENABLE", ddl, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("USING INDEX", ddl, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"DATADEVELOPER\"", ddl, StringComparison.Ordinal);
        Assert.DoesNotContain("datadeveloper.", ddl, StringComparison.Ordinal);
        Assert.Contains("create table orders", ddl, StringComparison.Ordinal);
        Assert.Contains("\n    customer_id number", ddl, StringComparison.Ordinal);
        Assert.Contains("order_total number(10,2) not null", ddl, StringComparison.Ordinal);
        Assert.Contains("status varchar2(30) default 'open' not null", ddl, StringComparison.Ordinal);
        Assert.Contains("primary key", ddl, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("timestamp default current_timestamp", ddl, StringComparison.OrdinalIgnoreCase);
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
