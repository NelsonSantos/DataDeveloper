using DataDeveloper.Data.Services;
using Xunit;

namespace DataDeveloper.Tests;

public class StatementSplitterTests
{
    [Fact]
    public void SplitStatements_IgnoresCommentOnlyTrailingBlock()
    {
        var sql = """
                  --begin
                      exec dbo.MyProc;
                  --end;
                  """;

        var statements = StatementSplitter.SplitStatements(sql);

        Assert.Single(statements);
        Assert.Contains("exec dbo.MyProc", statements[0]);
    }
}
