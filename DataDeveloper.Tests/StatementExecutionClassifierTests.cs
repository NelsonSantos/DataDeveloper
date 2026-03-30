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
}
