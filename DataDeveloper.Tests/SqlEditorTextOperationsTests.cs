using DataDeveloper.Services;
using DataDeveloper.Data.Enums;
using Xunit;

namespace DataDeveloper.Tests;

public sealed class SqlEditorTextOperationsTests
{
    [Fact]
    public void ToUpper_WithoutSelection_ReturnsNull()
    {
        var result = SqlEditorTextOperations.ToUpper("select 1", 0, 0);

        Assert.Null(result);
    }

    [Fact]
    public void ToLower_WithSelection_TransformsOnlySelection()
    {
        var result = SqlEditorTextOperations.ToLower("SELECT Name FROM dbo.Users", 0, 6);

        Assert.True(result.HasValue);
        Assert.Equal("select", result.Value.ReplacementText);
        Assert.Equal(0, result.Value.SelectionStart);
        Assert.Equal(6, result.Value.SelectionLength);
    }

    [Fact]
    public void Indent_WithSelection_IndentsAllTouchedLines()
    {
        var sql = "select a,\nfrom users";

        var result = SqlEditorTextOperations.Indent(sql, 0, sql.Length, "    ");

        Assert.Equal("    select a,\n    from users", result.ReplacementText);
        Assert.Equal(0, result.SelectionStart);
        Assert.Equal(result.ReplacementText.Length, result.SelectionLength);
    }

    [Fact]
    public void Unindent_WithoutSelection_RemovesOneIndentLevelFromCurrentLine()
    {
        var sql = "    select 1";

        var result = SqlEditorTextOperations.Unindent(sql, 4, 0, "    ");

        Assert.Equal("select 1", result.ReplacementText);
        Assert.Equal(0, result.SelectionStart);
        Assert.Equal(0, result.SelectionLength);
    }

    [Fact]
    public void Comment_CommentsAllTouchedLines()
    {
        var sql = "select 1\n  from dual";

        var result = SqlEditorTextOperations.Comment(sql, 0, sql.Length);

        Assert.Equal("-- select 1\n  -- from dual", result.ReplacementText);
    }

    [Fact]
    public void Uncomment_RemovesSqlLineCommentPrefix()
    {
        var sql = "-- select 1\n  -- from dual";

        var result = SqlEditorTextOperations.Uncomment(sql, 0, sql.Length);

        Assert.Equal("select 1\n  from dual", result.ReplacementText);
    }

    [Fact]
    public void Beautify_WithoutSelection_FormatsWholeDocument()
    {
        var sql = "select a, b from dbo.table where id = 1 and name = 'x'";

        var result = SqlEditorTextOperations.Beautify(sql, 7, 0, "    ", DatabaseType.SqlServer);

        Assert.Equal("select\n    a,\n    b\nfrom dbo.table\nwhere id = 1\n    and name = 'x'", result.ReplacementText);
        Assert.Equal(7, result.SelectionStart);
        Assert.Equal(0, result.SelectionLength);
    }

    [Fact]
    public void Beautify_UsesMySqlLexerForMySqlStatements()
    {
        var sql = "select id,name from users where status='A' and role='admin'";

        var result = SqlEditorTextOperations.Beautify(sql, 0, 0, "    ", DatabaseType.MySql);

        Assert.Equal("select\n    id,\n    name\nfrom users\nwhere status = 'A'\n    and role = 'admin'", result.ReplacementText);
    }
}
