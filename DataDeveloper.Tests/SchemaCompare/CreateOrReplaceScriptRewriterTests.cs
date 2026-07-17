using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Services.SchemaCompare;
using Xunit;

namespace DataDeveloper.Tests.SchemaCompare;

public class CreateOrReplaceScriptRewriterTests
{
    [Theory]
    [InlineData(SchemaCompareObjectType.View, "view")]
    [InlineData(SchemaCompareObjectType.Procedure, "procedure")]
    [InlineData(SchemaCompareObjectType.Function, "function")]
    public void SqlServer_RewritesLeadingCreateToCreateOrAlter(SchemaCompareObjectType objectType, string kind)
    {
        var ddl = $"create {kind} dbo.Widget as select 1";

        var result = CreateOrReplaceScriptRewriter.BuildChangedObjectScript(DatabaseType.SqlServer, objectType, "dbo.Widget", ddl);

        Assert.StartsWith($"create or alter {kind}", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SqlServer_KeywordMatchingIsCaseInsensitive()
    {
        var ddl = "CREATE   VIEW dbo.Widget AS select 1";

        var result = CreateOrReplaceScriptRewriter.BuildChangedObjectScript(DatabaseType.SqlServer, SchemaCompareObjectType.View, "dbo.Widget", ddl);

        Assert.StartsWith("CREATE   or alter VIEW", result);
    }

    [Theory]
    [InlineData(SchemaCompareObjectType.View, "view")]
    [InlineData(SchemaCompareObjectType.Procedure, "procedure")]
    [InlineData(SchemaCompareObjectType.Function, "function")]
    public void Oracle_RewritesLeadingCreateToCreateOrReplace(SchemaCompareObjectType objectType, string kind)
    {
        var ddl = $"create {kind} Widget as begin null; end;";

        var result = CreateOrReplaceScriptRewriter.BuildChangedObjectScript(DatabaseType.Oracle, objectType, "Widget", ddl);

        Assert.StartsWith($"create or replace {kind}", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Oracle_DoesNotDoubleRewriteAlreadyCreateOrReplaceDdl()
    {
        var ddl = "create or replace view Widget as select 1 from dual";

        var result = CreateOrReplaceScriptRewriter.BuildChangedObjectScript(DatabaseType.Oracle, SchemaCompareObjectType.View, "Widget", ddl);

        Assert.Equal(ddl, result);
    }

    [Fact]
    public void Postgres_View_RewritesLeadingCreateToCreateOrReplace()
    {
        var ddl = "create view public.widget as select 1";

        var result = CreateOrReplaceScriptRewriter.BuildChangedObjectScript(DatabaseType.PostgresSql, SchemaCompareObjectType.View, "public.widget", ddl);

        Assert.StartsWith("create or replace view", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Postgres_Function_RewritesLeadingCreateToCreateOrReplace()
    {
        var ddl = "create function public.widget_fn() returns int as $$ select 1 $$ language sql";

        var result = CreateOrReplaceScriptRewriter.BuildChangedObjectScript(DatabaseType.PostgresSql, SchemaCompareObjectType.Function, "public.widget_fn", ddl);

        Assert.StartsWith("create or replace function", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Postgres_Procedure_UsesDropIfExistsThenCreate_NotOrReplace()
    {
        var ddl = "create procedure public.widget_proc() as $$ begin end $$ language plpgsql";

        var result = CreateOrReplaceScriptRewriter.BuildChangedObjectScript(DatabaseType.PostgresSql, SchemaCompareObjectType.Procedure, "public.widget_proc", ddl);

        Assert.StartsWith("drop procedure if exists public.widget_proc;", result);
        Assert.Contains(ddl, result);
        Assert.DoesNotContain("or replace", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MySql_View_RewritesLeadingCreateToCreateOrReplace()
    {
        var ddl = "create view widget as select 1";

        var result = CreateOrReplaceScriptRewriter.BuildChangedObjectScript(DatabaseType.MySql, SchemaCompareObjectType.View, "widget", ddl);

        Assert.StartsWith("create or replace view", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MySql_Procedure_UsesDropIfExistsThenCreate()
    {
        var ddl = "create procedure widget_proc() begin end";

        var result = CreateOrReplaceScriptRewriter.BuildChangedObjectScript(DatabaseType.MySql, SchemaCompareObjectType.Procedure, "widget_proc", ddl);

        Assert.StartsWith("drop procedure if exists widget_proc;", result);
        Assert.Contains(ddl, result);
    }

    [Fact]
    public void MySql_Function_UsesDropIfExistsThenCreate()
    {
        var ddl = "create function widget_fn() returns int deterministic return 1";

        var result = CreateOrReplaceScriptRewriter.BuildChangedObjectScript(DatabaseType.MySql, SchemaCompareObjectType.Function, "widget_fn", ddl);

        Assert.StartsWith("drop function if exists widget_fn;", result);
        Assert.Contains(ddl, result);
    }

    [Fact]
    public void SqLite_View_UsesDropIfExistsThenCreate()
    {
        var ddl = "create view widget as select 1";

        var result = CreateOrReplaceScriptRewriter.BuildChangedObjectScript(DatabaseType.SqLite, SchemaCompareObjectType.View, "widget", ddl);

        Assert.StartsWith("drop view if exists widget;", result);
        Assert.Contains(ddl, result);
    }
}
