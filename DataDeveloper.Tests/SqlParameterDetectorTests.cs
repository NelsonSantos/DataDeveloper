using DataDeveloper.Services;
using Xunit;

namespace DataDeveloper.Tests;

public class SqlParameterDetectorTests
{
    [Fact]
    public void ExtractParameters_ReturnsNamedParameters_InOrder()
    {
        var sql = "select * from orders where id = @id and origin = @origin";

        var parameters = SqlParameterDetector.ExtractParameters(sql);

        Assert.Equal(["@id", "@origin"], parameters);
    }

    [Fact]
    public void ExtractParameters_IgnoresDoubleAtTokens()
    {
        var sql = "select @@version, @id";

        var parameters = SqlParameterDetector.ExtractParameters(sql);

        Assert.Equal(["@id"], parameters);
    }

    [Fact]
    public void ExtractParameters_IgnoresStringsAndComments()
    {
        var sql = """
                  select '@ignored', name
                  from orders
                  where id = @id
                  -- @ignoredComment
                  and note = 'email@test.com'
                  /* @ignoredBlock */
                  """;

        var parameters = SqlParameterDetector.ExtractParameters(sql);

        Assert.Equal(["@id"], parameters);
    }

    [Fact]
    public void ExtractParameters_IgnoresProcedureDeclarationParameters()
    {
        var sql = """
                  create procedure dbo.Teste
                      @id int,
                      @name varchar(50)
                  as
                  begin
                      select * from people where id = @id and name = @name and active = @active
                  end
                  """;

        var parameters = SqlParameterDetector.ExtractParameters(sql);

        Assert.Equal(["@id", "@name", "@active"], parameters);
    }

    [Fact]
    public void ExtractParameters_IgnoresLocallyDeclaredVariables()
    {
        var sql = """
                  declare @startDate datetime = '2026-01-01 00:00:00';
                  declare @endDate datetime = '2026-01-31 23:59:59';
                  exec [GetSalesReport] @startDate, @endDate;
                  select * from sales where id = @saleId;
                  """;

        var parameters = SqlParameterDetector.ExtractParameters(sql);

        Assert.Equal(["@saleId"], parameters);
    }
}
