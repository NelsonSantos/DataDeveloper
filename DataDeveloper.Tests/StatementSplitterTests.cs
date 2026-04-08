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

    [Fact]
    public void SplitStatements_IgnoresStandaloneOracleSlashDelimiter()
    {
        var sql = """
                  create or replace view open_orders as
                  select *
                  from orders;
                  /
                  """;

        var statements = StatementSplitter.SplitStatements(sql);

        Assert.Single(statements);
        Assert.DoesNotContain("/", statements[0]);
        Assert.Contains("create or replace view open_orders", statements[0]);
    }

    [Fact]
    public void SplitStatements_KeepsOracleRoutineBodiesUntilSlashDelimiter()
    {
        var sql = """
                  create or replace procedure mark_order_shipped
                  (
                      p_order_id in number
                  )
                  as
                  begin
                      update orders
                      set status = 'SHIPPED'
                      where order_id = p_order_id;
                  end;
                  /

                  create or replace function get_customer_total
                  (
                      p_customer_id in number
                  )
                  return number
                  as
                      v_total number(10, 2);
                  begin
                      select coalesce(sum(order_total), 0)
                      into v_total
                      from orders
                      where customer_id = p_customer_id;

                      return v_total;
                  end;
                  /
                  """;

        var statements = StatementSplitter.SplitStatements(sql);

        Assert.Equal(2, statements.Count);
        Assert.Contains("create or replace procedure mark_order_shipped", statements[0]);
        Assert.Contains("where order_id = p_order_id;", statements[0]);
        Assert.Contains("create or replace function get_customer_total", statements[1]);
        Assert.Contains("v_total number(10, 2);", statements[1]);
        Assert.DoesNotContain("/", statements[0]);
        Assert.DoesNotContain("/", statements[1]);
    }

    [Fact]
    public void SplitStatements_KeepsTerminalSemicolon_ForOracleAnonymousBlock()
    {
        var sql = """
                  begin "MARK_ORDER_SHIPPED"(:p_order_id); end;
                  """;

        var statements = StatementSplitter.SplitStatements(sql);

        var statement = Assert.Single(statements);
        Assert.Equal("""begin "MARK_ORDER_SHIPPED"(:p_order_id); end;""", statement);
    }

    [Fact]
    public void SplitStatements_KeepsOracleRoutineBodiesUntilSlashDelimiter_AfterLeadingComments()
    {
        var sql = """
                  -- deploy comment
                  create or replace procedure mark_order_shipped
                  as
                  begin
                      null;
                  end;
                  /
                  """;

        var statements = StatementSplitter.SplitStatements(sql);

        var statement = Assert.Single(statements);
        Assert.Contains("create or replace procedure mark_order_shipped", statement);
        Assert.DoesNotContain("/", statement);
    }

    [Fact]
    public void SplitStatements_KeepsTerminalSemicolon_ForOracleDeclareBlock()
    {
        var sql = """
                  declare v_order_id number := 1; begin null; end;
                  """;

        var statements = StatementSplitter.SplitStatements(sql);

        var statement = Assert.Single(statements);
        Assert.Equal("""declare v_order_id number := 1; begin null; end;""", statement);
    }
}
