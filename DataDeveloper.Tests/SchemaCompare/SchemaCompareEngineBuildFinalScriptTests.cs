using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Models.SchemaCompare;
using DataDeveloper.Data.Models.TableDesigner;
using DataDeveloper.Services.SchemaCompare;
using Xunit;

namespace DataDeveloper.Tests.SchemaCompare;

public class SchemaCompareEngineBuildFinalScriptTests
{
    [Fact]
    public void BuildFinalScript_OrdersCategoriesTablesThenViewsThenFunctionsThenProcedures()
    {
        var results = new[]
        {
            Result(SchemaCompareObjectType.Procedure, "MyProc", "create procedure MyProc as begin end;"),
            Result(SchemaCompareObjectType.Function, "MyFunc", "create function MyFunc() returns int as begin return 1; end;"),
            Result(SchemaCompareObjectType.View, "MyView", "create view MyView as select 1;"),
            NewTableResult("MyTable", "create table MyTable (Id int);")
        };

        var script = SchemaCompareEngine.BuildFinalScript("Source", "Destination", DatabaseType.SqlServer, results);

        var tableIndex = script.IndexOf("create table MyTable", StringComparison.Ordinal);
        var viewIndex = script.IndexOf("create view MyView", StringComparison.Ordinal);
        var functionIndex = script.IndexOf("create function MyFunc", StringComparison.Ordinal);
        var procedureIndex = script.IndexOf("create procedure MyProc", StringComparison.Ordinal);

        Assert.True(tableIndex < viewIndex);
        Assert.True(viewIndex < functionIndex);
        Assert.True(functionIndex < procedureIndex);
    }

    [Fact]
    public void BuildFinalScript_WithNoIncludedResults_ReturnsBannerAndNoChangesMessage()
    {
        var script = SchemaCompareEngine.BuildFinalScript("Source", "Destination", DatabaseType.SqlServer, Array.Empty<SchemaCompareObjectResult>());

        Assert.Contains("No changes selected", script);
        Assert.Contains("Source: Source", script);
        Assert.Contains("Destination: Destination", script);
    }

    [Fact]
    public void BuildFinalScript_PrependsHeaderCommentForEachIncludedObject()
    {
        var results = new[] { Result(SchemaCompareObjectType.View, "MyView", "create view MyView as select 1;") };

        var script = SchemaCompareEngine.BuildFinalScript("Source", "Destination", DatabaseType.SqlServer, results);

        Assert.Contains("-- ==== View MyView (Changed) ====", script);
    }

    [Fact]
    public void BuildFinalScript_NeverExecutesAndAlwaysWarnsToReview()
    {
        var script = SchemaCompareEngine.BuildFinalScript("Source", "Destination", DatabaseType.SqlServer, Array.Empty<SchemaCompareObjectResult>());

        Assert.Contains("Nothing has been run against the destination", script);
    }

    [Fact]
    public void BuildFinalScript_ForSqlServer_SeparatesBlocksWithGo()
    {
        var results = new[]
        {
            NewTableResult("MyTable", "create table MyTable (Id int);"),
            Result(SchemaCompareObjectType.Function, "MyFunc", "create function MyFunc() returns int as begin return 1 end")
        };

        var script = SchemaCompareEngine.BuildFinalScript("Source", "Destination", DatabaseType.SqlServer, results);

        var tableIndex = script.IndexOf("create table MyTable", StringComparison.Ordinal);
        var goIndex = script.IndexOf($"{Environment.NewLine}GO{Environment.NewLine}", StringComparison.Ordinal);
        var functionIndex = script.IndexOf("create function MyFunc", StringComparison.Ordinal);

        Assert.True(tableIndex < goIndex);
        Assert.True(goIndex < functionIndex);
    }

    [Fact]
    public void BuildFinalScript_ForOracle_SeparatesBlocksWithSlash()
    {
        var results = new[]
        {
            NewTableResult("MyTable", "create table MyTable (Id number);"),
            Result(SchemaCompareObjectType.Function, "MyFunc", "create or replace function MyFunc return number is begin return 1; end;")
        };

        var script = SchemaCompareEngine.BuildFinalScript("Source", "Destination", DatabaseType.Oracle, results);

        var tableIndex = script.IndexOf("create table MyTable", StringComparison.Ordinal);
        var slashIndex = script.IndexOf($"{Environment.NewLine}/{Environment.NewLine}", StringComparison.Ordinal);
        var functionIndex = script.IndexOf("create or replace function MyFunc", StringComparison.Ordinal);

        Assert.True(tableIndex < slashIndex);
        Assert.True(slashIndex < functionIndex);
    }

    [Fact]
    public void BuildFinalScript_ForPostgres_DoesNotInsertGoOrSlashSeparators()
    {
        var results = new[]
        {
            NewTableResult("MyTable", "create table MyTable (Id int);"),
            Result(SchemaCompareObjectType.Function, "MyFunc", "create function MyFunc() returns int as $$ select 1 $$ language sql;")
        };

        var script = SchemaCompareEngine.BuildFinalScript("Source", "Destination", DatabaseType.PostgresSql, results);

        Assert.DoesNotContain($"{Environment.NewLine}GO{Environment.NewLine}", script);
        Assert.DoesNotContain($"{Environment.NewLine}/{Environment.NewLine}", script);
    }

    private static SchemaCompareObjectResult Result(SchemaCompareObjectType objectType, string name, string script)
    {
        return new SchemaCompareObjectResult
        {
            ObjectType = objectType,
            Name = name,
            Status = SchemaCompareResultStatus.Changed,
            Script = script,
            IsIncludedByDefault = true
        };
    }

    private static SchemaCompareObjectResult NewTableResult(string name, string script)
    {
        return new SchemaCompareObjectResult
        {
            ObjectType = SchemaCompareObjectType.Table,
            Name = name,
            Status = SchemaCompareResultStatus.New,
            Script = script,
            IsIncludedByDefault = true,
            NewTableDefinition = new TableDefinition { TableName = name }
        };
    }
}
