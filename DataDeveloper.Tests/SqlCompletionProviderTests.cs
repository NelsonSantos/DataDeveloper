using DataDeveloper.Services;
using Xunit;

namespace DataDeveloper.Tests;

public class SqlCompletionProviderTests
{
    [Fact]
    public void AutoRequest_AfterFrom_ReturnsObjectsTrigger()
    {
        var sql = "select * from ";

        var request = SqlCompletionProvider.GetAutoCompletionRequest(sql, sql.Length, "m");

        Assert.NotNull(request);
        Assert.Equal(CompletionTrigger.Objects, request!.Context.Trigger);
        Assert.Equal(SqlCompletionProvider.SqlClause.From, request.Context.Clause);
    }

    [Fact]
    public void ManualRequest_AfterSelect_ReturnsColumnsTrigger()
    {
        var sql = "select ";

        var request = SqlCompletionProvider.GetManualCompletionRequest(sql, sql.Length);

        Assert.Equal(CompletionTrigger.Columns, request.Context.Trigger);
        Assert.Equal(SqlCompletionProvider.SqlClause.Select, request.Context.Clause);
    }

    [Fact]
    public void AutoRequest_InsertColumnList_OpenParen_ReturnsColumnsTrigger()
    {
        var sql = "insert into clientes (";

        var request = SqlCompletionProvider.GetAutoCompletionRequest(sql, sql.Length, "(");

        Assert.NotNull(request);
        Assert.True(request!.Context.IsInsideInsertColumnList);
        Assert.Equal(CompletionTrigger.Columns, request.Context.Trigger);
    }

    [Fact]
    public void AutoRequest_InsertValuesOpenParen_DoesNotReturnColumnsTrigger()
    {
        var sql = "insert into clientes (id, nome) values (";

        var request = SqlCompletionProvider.GetAutoCompletionRequest(sql, sql.Length, "(");

        Assert.Null(request);
    }

    [Fact]
    public void AutoRequest_UpdateSetComma_ReturnsColumnsTrigger()
    {
        var sql = "update clientes set nome = @nome,";

        var request = SqlCompletionProvider.GetAutoCompletionRequest(sql, sql.Length, ",");

        Assert.NotNull(request);
        Assert.True(request!.Context.IsInsideUpdateSetList);
        Assert.Equal(CompletionTrigger.Columns, request.Context.Trigger);
        Assert.Equal(SqlCompletionProvider.SqlClause.Set, request.Context.Clause);
    }

    [Fact]
    public void AutoRequest_AfterJoin_ReturnsObjectsTrigger()
    {
        var sql = "select * from clientes c inner join ";

        var request = SqlCompletionProvider.GetAutoCompletionRequest(sql, sql.Length, "p");

        Assert.NotNull(request);
        Assert.Equal(CompletionTrigger.Objects, request!.Context.Trigger);
        Assert.Equal(SqlCompletionProvider.SqlClause.Join, request.Context.Clause);
    }

    [Fact]
    public void ManualRequest_AfterDeleteWhere_ReturnsColumnsTrigger()
    {
        var sql = "delete from clientes where ";

        var request = SqlCompletionProvider.GetManualCompletionRequest(sql, sql.Length);

        Assert.Equal(CompletionTrigger.Columns, request.Context.Trigger);
        Assert.Equal(SqlCompletionProvider.SqlClause.Where, request.Context.Clause);
    }

    [Fact]
    public void ManualRequest_AfterAliasDot_ReturnsColumnsTrigger()
    {
        var sql = "select c. from clientes c";
        var caretOffset = "select c.".Length;

        var request = SqlCompletionProvider.GetManualCompletionRequest(sql, caretOffset);

        Assert.Equal(CompletionTrigger.Columns, request.Context.Trigger);
        Assert.Equal("c", request.Context.ObjectNameBeforeDot);
    }

    [Fact]
    public void AutoRequest_InsertColumnListCommaWithSpace_KeepsColumnsTrigger()
    {
        var sql = "insert into clientes (id, ";

        var request = SqlCompletionProvider.GetManualCompletionRequest(sql, sql.Length);

        Assert.True(request.Context.IsInsideInsertColumnList);
        Assert.Equal(CompletionTrigger.Columns, request.Context.Trigger);
        Assert.Equal(SqlCompletionProvider.SqlClause.Into, request.Context.Clause);
    }
}
