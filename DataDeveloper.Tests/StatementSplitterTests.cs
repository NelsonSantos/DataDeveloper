using DataDeveloper.Data.Services;
using DataDeveloper.Data.Enums;
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

        var statements = StatementSplitter.SplitStatements(sql, DatabaseType.SqlServer);

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

        var statements = StatementSplitter.SplitStatements(sql, DatabaseType.Oracle);

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

        var statements = StatementSplitter.SplitStatements(sql, DatabaseType.Oracle);

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

        var statements = StatementSplitter.SplitStatements(sql, DatabaseType.Oracle);

        var statement = Assert.Single(statements);
        Assert.Equal("""begin "MARK_ORDER_SHIPPED"(:p_order_id); end;""", statement);
    }

    [Fact]
    public void SplitStatements_SplitsBeginTransactionScript()
    {
        var sql = """
                  begin transaction;
                  insert into items(name) values ('scripted');
                  commit;
                  select count(*) from items;
                  """;

        var statements = StatementSplitter.SplitStatements(sql, DatabaseType.SqlServer);

        Assert.Equal(4, statements.Count);
        Assert.Equal("begin transaction", statements[0]);
        Assert.StartsWith("insert into items", statements[1], StringComparison.OrdinalIgnoreCase);
        Assert.Equal("commit", statements[2]);
        Assert.StartsWith("select count(*)", statements[3], StringComparison.OrdinalIgnoreCase);
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

        var statements = StatementSplitter.SplitStatements(sql, DatabaseType.Oracle);

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

        var statements = StatementSplitter.SplitStatements(sql, DatabaseType.Oracle);

        var statement = Assert.Single(statements);
        Assert.Equal("""declare v_order_id number := 1; begin null; end;""", statement);
    }

    [Fact]
    public void SplitStatements_KeepsSqlServerProcedureBody_WhenBodyContainsCaseExpressions()
    {
        var sql = """
                  create or alter procedure dbo.GetOrderSummary(@orderId uniqueidentifier = null)
                  as
                  begin
                      set nocount on;

                      declare @pointOfSaleId uniqueidentifier;

                      select
                          FullAddress = ps.AddressLine1 + case when ps.AddressLine2 <> '' then ' (' + ps.AddressLine2 + ')' end
                      from TenantPointOfSale ps
                      where ps.Id = @pointOfSaleId;

                      select
                          CustomerDocument = case o.CustomerDocumentType when 1 then 'CPF' else 'CNPJ' end
                      from TenantOrder o
                      where o.Id = @orderId;
                  end

                  go

                  select 1;
                  """;

        var statements = StatementSplitter.SplitStatements(sql, DatabaseType.SqlServer);

        Assert.Equal(2, statements.Count);
        Assert.StartsWith("create or alter procedure", statements[0], StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("end", statements[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("where o.Id = @orderId", statements[0]);
        Assert.Equal("select 1", statements[1]);
    }

    [Fact]
    public void SplitStatements_KeepsSqlServerTryCatchBody_WhenBodyContainsCaseExpressions()
    {
        var sql = """
                  create procedure dbo.Classify
                  as
                  begin
                      begin try
                          select case when 1 = 1 then 'a' else 'b' end;
                      end try
                      begin catch
                          select error_message();
                      end catch;
                  end
                  go
                  """;

        var statements = StatementSplitter.SplitStatements(sql, DatabaseType.SqlServer);

        var statement = Assert.Single(statements);
        Assert.EndsWith("end", statement, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SplitStatements_KeepsMySqlProcedureBody_WhenBodyContainsCaseAndIfStatements()
    {
        var sql = """
                  create procedure classify_order(in p_order_id int)
                  begin
                      declare v_kind varchar(10);
                      select case status when 1 then 'open' else 'closed' end into v_kind
                      from orders
                      where id = p_order_id;
                      if v_kind = 'open' then
                          update orders set touched = 1 where id = p_order_id;
                      end if;
                      case v_kind
                          when 'open' then select 1;
                          else select 2;
                      end case;
                  end;
                  select 1;
                  """;

        var statements = StatementSplitter.SplitStatements(sql, DatabaseType.MySql);

        Assert.Equal(2, statements.Count);
        Assert.StartsWith("create procedure classify_order", statements[0], StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("end", statements[0], StringComparison.OrdinalIgnoreCase);
        Assert.Equal("select 1", statements[1]);
    }

    [Fact]
    public void SplitStatements_KeepsSqliteTriggerBody_WhenBodyContainsCaseExpression()
    {
        var sql = """
                  create trigger orders_touch after update on orders
                  begin
                      update orders set kind = case when new.status = 1 then 'open' else 'closed' end where id = new.id;
                      update orders set touched = 1 where id = new.id;
                  end;
                  select 1;
                  """;

        var statements = StatementSplitter.SplitStatements(sql, DatabaseType.SqLite);

        Assert.Equal(2, statements.Count);
        Assert.StartsWith("create trigger orders_touch", statements[0], StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("end", statements[0], StringComparison.OrdinalIgnoreCase);
        Assert.Equal("select 1", statements[1]);
    }

    [Fact]
    public void SplitStatements_KeepsOracleProcedureBody_WithoutSlashDelimiter_WhenBodyContainsCaseAndIf()
    {
        var sql = """
                  create or replace procedure classify_order(p_order_id in number)
                  as
                      v_kind varchar2(10);
                  begin
                      select case status when 1 then 'open' else 'closed' end into v_kind
                      from orders
                      where order_id = p_order_id;
                      if v_kind = 'open' then
                          null;
                      end if;
                  end;
                  """;

        var statements = StatementSplitter.SplitStatements(sql, DatabaseType.Oracle);

        var statement = Assert.Single(statements);
        Assert.EndsWith("end;", statement, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(DatabaseType.SqlServer)]
    [InlineData(DatabaseType.MySql)]
    [InlineData(DatabaseType.PostgresSql)]
    [InlineData(DatabaseType.Oracle)]
    [InlineData(DatabaseType.SqLite)]
    public void SplitStatements_SplitsTopLevelStatements_ThatContainCaseExpressions(DatabaseType databaseType)
    {
        var sql = """
                  select case when 1 = 1 then 'a' else 'b' end as kind from orders;
                  select 2 from orders;
                  """;

        var statements = StatementSplitter.SplitStatements(sql, databaseType);

        Assert.Equal(2, statements.Count);
        Assert.StartsWith("select case", statements[0], StringComparison.OrdinalIgnoreCase);
        Assert.Equal("select 2 from orders", statements[1]);
    }
}
