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

        Assert.Empty(parameters);
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

    [Fact]
    public void ExtractParameters_IgnoresVariablesInsideFunctionBody()
    {
        var sql = """
                  CREATE FUNCTION [dbo].[GetRecordLocatorFromId] (
                      @Id BIGINT
                  )
                  RETURNS VARCHAR(6)
                  AS
                  BEGIN
                      DECLARE
                          @Size INT = 6,
                          @Chars VARCHAR(33) = '0123456789ABCDEFGHJKMNPQRSTUVWXYZ',
                          @Mod INT = 33,
                          @Max BIGINT = 1291467969,
                          @I INT,
                          @Pos INT,
                          @Locator NVARCHAR(MAX)

                      SELECT
                          @Id = @Id % @Max,
                          @I = 1,
                          @Locator = ''

                      RETURN @Locator
                  END
                  """;

        var parameters = SqlParameterDetector.ExtractParameters(sql);

        Assert.Empty(parameters);
    }

    [Fact]
    public void ExtractParameters_DetectsExecParameters_AfterSqlServerBatchSeparator()
    {
        var sql = """
                  create procedure dbo.Teste
                      @id int
                  as
                  begin
                      select @id;
                  end
                  go
                  exec dbo.Teste @externalId;
                  """;

        var parameters = SqlParameterDetector.ExtractParameters(sql);

        Assert.Equal(["@externalId"], parameters);
    }

    [Fact]
    public void ExtractParameters_IgnoresProcedureDeclarationWithExtraWhitespace()
    {
        var sql = """
                  CREATE   PROCEDURE [dbo].[GetBusMarketsByArrival] (@ArrivalLocationSlug VARCHAR(100))
                  AS
                  BEGIN
                      SELECT 1
                      WHERE 1 = CASE WHEN @ArrivalLocationSlug IS NULL THEN 0 ELSE 1 END
                  END
                  """;

        var parameters = SqlParameterDetector.ExtractParameters(sql);

        Assert.Empty(parameters);
    }

    [Fact]
    public void ExtractParameters_IgnoresProcedureBodyParameters_ForSqlServerProcedureSource()
    {
        var sql = """
                  CREATE     procedure [dbo].[GetSalesByPointOfSaleReport](
                       @startDate datetime
                       , @endDate datetime
                       , @pointOfSaleIds varchar(max)
                   )
                   as
                   begin

                       set nocount on;

                       declare @defaultGuid uniqueidentifier = '00000000-0000-0000-0000-000000000000';

                       select
                           @startDate = dbo.ResolveDateTimeValueStart(@startDate),
                           @endDate = dbo.ResolveDateTimeValueEnd(@endDate);

                       declare @pointOfSaleTable table(
                           recordNumber int,
                           pointOfSaleId uniqueidentifier,
                           pointOfSaleName varchar(100),
                           TimeZoneOffset int
                       );

                       insert into @pointOfSaleTable
                       select
                           recordNumber = row_number() over (order by pos.name)
                           , pointOfSaleId
                           , pointOfSaleName = upper(pos.Name)
                           , pos.TimeZoneOffset
                       from (
                           select
                               pointOfSaleId = cast(value as uniqueidentifier)
                           from string_split(@pointOfSaleIds, ',')
                       ) p
                       inner join TenantPointOfSale pos on p.pointOfSaleId = pos.id;

                       insert into @pointOfSaleTable
                       select recordNumber = 999999, pointOfSaleId = @defaultGuid, pointOfSaleName = 'TOTAL GERAL', TimeZoneOffset = 0;

                       select
                           [startDate] = @startDate,
                           [endDate] = @endDate;

                       SELECT
                           DL.Name DepartureStationName,
                           AL.Name ArrivalStationName,
                           C.Name CompanyName,
                           T.ClassOfService ClassOfServiceName,
                           T.DepartureDateTime,
                           T.ArrivalDateTime,
                           T.PassengerName,
                           T.PassengerDocumentNumber + ' (' + CASE T.PassengerDocumentType WHEN 1 THEN 'CPF' WHEN 2 THEN 'RG' ELSE 'OUTRO' END + ')' PassengerDocument,
                           T.SeatIdentifier,
                           'Emissão' Statement,
                           DATEADD(MINUTE, P.TimeZoneOffset, T.IssuanceDateTimeUtc) StatementDateTime,
                           p.pointOfSaleName,
                           T.SubtotalAmount SubtotalAmount,
                           T.DiscountAmount DiscountAmount,
                           TPS.CommissionAmount,
                           OI.ServiceFeeAmount,
                           OI.TotalAmount
                       FROM
                           TenantCashier TC WITH(NOLOCK)
                           INNER JOIN TenantPointOfSaleStatement TPS WITH(NOLOCK) ON TC.Id = TPS.CashierId
                           INNER JOIN TenantOrderItem OI ON TPS.OrderItemId = OI.Id
                           INNER JOIN TenantTicket T WITH(NOLOCK) ON OI.ReferenceId = T.Id
                           INNER JOIN @pointOfSaleTable P ON T.PointOfSaleId = P.PointOfSaleId
                           INNER JOIN TenantCompany C ON T.CompanyId = C.Id
                           INNER JOIN TenantLocation DL ON T.DepartureLocationId = DL.Id
                           INNER JOIN TenantLocation AL ON T.ArrivalLocationId = AL.Id
                       WHERE
                           tps.StatementDateTimeOffSet between @startDate and @endDate
                           AND TPS.[StatementType] = 1
                       ORDER BY p.pointOfSaleName, StatementDateTime;

                   end
                  """;

        var parameters = SqlParameterDetector.ExtractParameters(sql);

        Assert.Empty(parameters);
    }

    [Fact]
    public void ExtractParameters_ReturnsOracleBindVariables()
    {
        var sql = "select * from orders where id = :id and status = :status";

        var parameters = SqlParameterDetector.ExtractParameters(sql);

        Assert.Equal([":id", ":status"], parameters);
    }

    [Fact]
    public void ExtractParameters_IgnoresPostgresCastTokens()
    {
        var sql = "select created_at::date from orders where id = :id";

        var parameters = SqlParameterDetector.ExtractParameters(sql);

        Assert.Equal([":id"], parameters);
    }
}
