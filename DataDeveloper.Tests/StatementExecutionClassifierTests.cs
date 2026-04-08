using DataDeveloper.Data.Services;
using Xunit;

namespace DataDeveloper.Tests;

public class StatementExecutionClassifierTests
{
    [Theory]
    [InlineData("exec dbo.MyProc")]
    [InlineData(" execute dbo.MyProc")]
    [InlineData("call my_proc()")]
    [InlineData("begin exec dbo.MyProc; end;")]
    [InlineData("begin declare @a int = 1; call my_proc(); end;")]
    [InlineData("--begin\nexec dbo.MyProc;\n--end;")]
    public void RequiresMaterialization_ReturnsTrue_ForRoutineInvocations(string statement)
    {
        Assert.True(StatementExecutionClassifier.RequiresMaterialization(statement));
    }

    [Theory]
    [InlineData("select * from orders")]
    [InlineData(" update orders set status = 1")]
    [InlineData("insert into orders(id) values (1)")]
    public void RequiresMaterialization_ReturnsFalse_ForRegularStatements(string statement)
    {
        Assert.False(StatementExecutionClassifier.RequiresMaterialization(statement));
    }

    [Theory]
    [InlineData("create table dbo.Test(Id int)")]
    [InlineData(" alter table dbo.Test add Name varchar(50)")]
    [InlineData("drop view dbo.ActiveCustomers")]
    [InlineData("truncate table dbo.Log")]
    [InlineData("-- comment\nrename table old_name to new_name")]
    public void RequiresSchemaRefresh_ReturnsTrue_ForDdlStatements(string statement)
    {
        Assert.True(StatementExecutionClassifier.RequiresSchemaRefresh(statement));
    }

    [Theory]
    [InlineData("select * from orders")]
    [InlineData("insert into orders(id) values (1)")]
    [InlineData("update orders set status = 1")]
    [InlineData("delete from orders where id = 1")]
    [InlineData("exec dbo.MyProc")]
    public void RequiresSchemaRefresh_ReturnsFalse_ForNonDdlStatements(string statement)
    {
        Assert.False(StatementExecutionClassifier.RequiresSchemaRefresh(statement));
    }

    [Theory]
    [InlineData("create table dbo.Test(Id int)", SchemaRefreshAction.Create, SchemaObjectType.Table, "dbo.Test")]
    [InlineData("alter view dbo.ActiveCustomers as select 1", SchemaRefreshAction.Alter, SchemaObjectType.View, "dbo.ActiveCustomers")]
    [InlineData("drop procedure dbo.MyProc", SchemaRefreshAction.Drop, SchemaObjectType.Procedure, "dbo.MyProc")]
    [InlineData("truncate table dbo.Log", SchemaRefreshAction.Alter, SchemaObjectType.Table, "dbo.Log")]
    public void ParseSchemaRefreshTarget_ReturnsExpectedTarget(
        string statement,
        SchemaRefreshAction expectedAction,
        SchemaObjectType expectedObjectType,
        string expectedName)
    {
        var target = StatementExecutionClassifier.ParseSchemaRefreshTarget(statement);

        Assert.NotNull(target);
        Assert.Equal(expectedAction, target!.Action);
        Assert.Equal(expectedObjectType, target.ObjectType);
        Assert.Equal(expectedName, target.ObjectName);
    }

    [Fact]
    public void ParseSchemaRefreshTarget_ReturnsUnknownTarget_ForRename()
    {
        var target = StatementExecutionClassifier.ParseSchemaRefreshTarget("rename table old_name to new_name");

        Assert.NotNull(target);
        Assert.Equal(SchemaRefreshAction.Unknown, target!.Action);
        Assert.Equal(SchemaObjectType.Unknown, target.ObjectType);
        Assert.Null(target.ObjectName);
    }
}
