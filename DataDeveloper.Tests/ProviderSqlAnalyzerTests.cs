using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Services;
using Xunit;

namespace DataDeveloper.Tests;

public class ProviderSqlAnalyzerTests
{
    [Theory]
    [InlineData(DatabaseType.SqlServer)]
    [InlineData(DatabaseType.MySql)]
    [InlineData(DatabaseType.PostgresSql)]
    [InlineData(DatabaseType.Oracle)]
    [InlineData(DatabaseType.SqLite)]
    public void SplitStatements_UsesProviderLexer_ForSupportedProviders(DatabaseType databaseType)
    {
        var analyzer = CreateAnalyzer(databaseType);

        var statements = analyzer.SplitStatements("""
                                                  select * from items;
                                                  update items set name = 'changed' where id = 1;
                                                  commit;
                                                  """);

        Assert.Equal(3, statements.Count);
        Assert.StartsWith("select", statements[0], StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("update", statements[1], StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("commit", statements[2], StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(DatabaseType.SqlServer)]
    [InlineData(DatabaseType.MySql)]
    [InlineData(DatabaseType.PostgresSql)]
    [InlineData(DatabaseType.Oracle)]
    [InlineData(DatabaseType.SqLite)]
    public void ClassifierMethods_AreAvailableThroughProviderAnalyzer(DatabaseType databaseType)
    {
        var analyzer = CreateAnalyzer(databaseType);

        Assert.True(analyzer.IsDmlStatement("insert into items(id) values (1)"));
        Assert.True(analyzer.RequiresSchemaRefresh("create table items(id int)"));
        Assert.False(analyzer.RequiresMaterialization("select * from items"));
    }

    [Theory]
    [InlineData(DatabaseType.SqlServer, "exec dbo.SyncCustomer", true)]
    [InlineData(DatabaseType.SqlServer, "begin exec dbo.SyncCustomer; end;", true)]
    [InlineData(DatabaseType.SqlServer, "begin transaction", false)]
    [InlineData(DatabaseType.MySql, "call sync_customer()", true)]
    [InlineData(DatabaseType.MySql, "begin", false)]
    [InlineData(DatabaseType.PostgresSql, "call sync_customer()", true)]
    [InlineData(DatabaseType.PostgresSql, "begin", false)]
    [InlineData(DatabaseType.Oracle, "call sync_customer()", true)]
    [InlineData(DatabaseType.Oracle, "begin call sync_customer(); end;", true)]
    [InlineData(DatabaseType.Oracle, "begin null; end;", false)]
    [InlineData(DatabaseType.SqLite, "call sync_customer()", false)]
    public void RequiresMaterialization_UsesProviderRules(DatabaseType databaseType, string statement, bool expected)
    {
        var analyzer = CreateAnalyzer(databaseType);

        Assert.Equal(expected, analyzer.RequiresMaterialization(statement));
    }

    [Theory]
    [InlineData(DatabaseType.SqlServer, "begin transaction", true)]
    [InlineData(DatabaseType.SqlServer, "begin tran", true)]
    [InlineData(DatabaseType.SqlServer, "start transaction", false)]
    [InlineData(DatabaseType.MySql, "start transaction", true)]
    [InlineData(DatabaseType.MySql, "begin", true)]
    [InlineData(DatabaseType.PostgresSql, "start transaction", true)]
    [InlineData(DatabaseType.PostgresSql, "begin", true)]
    [InlineData(DatabaseType.Oracle, "begin", false)]
    [InlineData(DatabaseType.Oracle, "begin transaction", false)]
    [InlineData(DatabaseType.SqLite, "begin", true)]
    [InlineData(DatabaseType.SqLite, "begin immediate", true)]
    public void IsBeginTransactionStatement_UsesProviderRules(DatabaseType databaseType, string statement, bool expected)
    {
        var analyzer = CreateAnalyzer(databaseType);

        Assert.Equal(expected, analyzer.IsBeginTransactionStatement(statement));
    }

    [Theory]
    [InlineData(DatabaseType.SqlServer, "commit", true)]
    [InlineData(DatabaseType.MySql, "rollback", true)]
    [InlineData(DatabaseType.PostgresSql, "start transaction", true)]
    [InlineData(DatabaseType.Oracle, "commit", true)]
    [InlineData(DatabaseType.Oracle, "begin", false)]
    [InlineData(DatabaseType.SqLite, "begin exclusive", true)]
    public void IsTransactionControlStatement_UsesProviderRules(DatabaseType databaseType, string statement, bool expected)
    {
        var analyzer = CreateAnalyzer(databaseType);

        Assert.Equal(expected, analyzer.IsTransactionControlStatement(statement));
    }

    [Theory]
    [InlineData(DatabaseType.MySql, "replace into items(id, name) values (1, 'a')", true)]
    [InlineData(DatabaseType.SqLite, "replace into items(id, name) values (1, 'a')", true)]
    [InlineData(DatabaseType.SqlServer, "replace into items(id, name) values (1, 'a')", false)]
    [InlineData(DatabaseType.PostgresSql, "replace into items(id, name) values (1, 'a')", false)]
    [InlineData(DatabaseType.Oracle, "replace into items(id, name) values (1, 'a')", false)]
    public void IsDmlStatement_UsesProviderRules(DatabaseType databaseType, string statement, bool expected)
    {
        var analyzer = CreateAnalyzer(databaseType);

        Assert.Equal(expected, analyzer.IsDmlStatement(statement));
    }

    [Theory]
    [InlineData(DatabaseType.SqlServer, "create or alter procedure dbo.SyncCustomer as select 1", SchemaRefreshAction.Create, SchemaObjectType.Procedure, "dbo.SyncCustomer")]
    [InlineData(DatabaseType.SqlServer, "drop table if exists dbo.TempImport", SchemaRefreshAction.Drop, SchemaObjectType.Table, "dbo.TempImport")]
    [InlineData(DatabaseType.MySql, "create temporary table TempImport(id int)", SchemaRefreshAction.Create, SchemaObjectType.Table, "TempImport")]
    [InlineData(DatabaseType.MySql, "drop view if exists active_customers", SchemaRefreshAction.Drop, SchemaObjectType.View, "active_customers")]
    [InlineData(DatabaseType.PostgresSql, "create or replace function public.normalize_name() returns text as $$ select 'x' $$ language sql", SchemaRefreshAction.Create, SchemaObjectType.Function, "public.normalize_name")]
    [InlineData(DatabaseType.PostgresSql, "create materialized view public.customer_totals as select 1", SchemaRefreshAction.Create, SchemaObjectType.View, "public.customer_totals")]
    [InlineData(DatabaseType.Oracle, "create or replace editionable procedure app.sync_customer as begin null; end;", SchemaRefreshAction.Create, SchemaObjectType.Procedure, "app.sync_customer")]
    [InlineData(DatabaseType.SqLite, "create temp table cache_items(id integer)", SchemaRefreshAction.Create, SchemaObjectType.Table, "cache_items")]
    public void ParseSchemaRefreshTarget_HandlesProviderDdlForms(
        DatabaseType databaseType,
        string statement,
        SchemaRefreshAction expectedAction,
        SchemaObjectType expectedObjectType,
        string expectedObjectName)
    {
        var analyzer = CreateAnalyzer(databaseType);

        var target = analyzer.ParseSchemaRefreshTarget(statement);

        Assert.NotNull(target);
        Assert.Equal(expectedAction, target!.Action);
        Assert.Equal(expectedObjectType, target.ObjectType);
        Assert.Equal(expectedObjectName, target.ObjectName);
    }

    [Theory]
    [InlineData(DatabaseType.SqlServer, "create table dbo.Test(Id int)", SchemaRefreshAction.Create, SchemaObjectType.Table, "dbo.Test")]
    [InlineData(DatabaseType.SqlServer, "alter view dbo.ActiveCustomers as select 1", SchemaRefreshAction.Alter, SchemaObjectType.View, "dbo.ActiveCustomers")]
    [InlineData(DatabaseType.SqlServer, "drop procedure dbo.MyProc", SchemaRefreshAction.Drop, SchemaObjectType.Procedure, "dbo.MyProc")]
    [InlineData(DatabaseType.SqlServer, "truncate table dbo.Log", SchemaRefreshAction.Alter, SchemaObjectType.Table, "dbo.Log")]
    public void ParseSchemaRefreshTarget_HandlesStandardDdlForms(
        DatabaseType databaseType,
        string statement,
        SchemaRefreshAction expectedAction,
        SchemaObjectType expectedObjectType,
        string expectedObjectName)
    {
        var analyzer = CreateAnalyzer(databaseType);

        var target = analyzer.ParseSchemaRefreshTarget(statement);

        Assert.NotNull(target);
        Assert.Equal(expectedAction, target!.Action);
        Assert.Equal(expectedObjectType, target.ObjectType);
        Assert.Equal(expectedObjectName, target.ObjectName);
    }

    [Fact]
    public void ParseSchemaRefreshTarget_ReturnsUnknownTarget_ForRename()
    {
        var analyzer = new MySqlSqlAnalyzer();

        var target = analyzer.ParseSchemaRefreshTarget("rename table old_name to new_name");

        Assert.NotNull(target);
        Assert.Equal(SchemaRefreshAction.Unknown, target!.Action);
        Assert.Equal(SchemaObjectType.Unknown, target.ObjectType);
        Assert.Null(target.ObjectName);
    }

    private static ProviderSqlAnalyzer CreateAnalyzer(DatabaseType databaseType)
    {
        return databaseType switch
        {
            DatabaseType.SqlServer => new SqlServerSqlAnalyzer(),
            DatabaseType.MySql => new MySqlSqlAnalyzer(),
            DatabaseType.PostgresSql => new PostgresSqlAnalyzer(),
            DatabaseType.Oracle => new OracleSqlAnalyzer(),
            DatabaseType.SqLite => new SqLiteSqlAnalyzer(),
            _ => new ProviderSqlAnalyzer(databaseType)
        };
    }
}
