using DataDeveloper.Data.Providers.SqLite;
using Xunit;

namespace DataDeveloper.Tests.Providers;

public class SqLiteCheckConstraintParserTests
{
    [Fact]
    public void Parse_ReadsColumnAndTableChecksInOrder()
    {
        var checks = SqLiteCheckConstraintParser.Parse("""
            CREATE TABLE order_items (
                id integer primary key,
                quantity integer not null check (quantity > 0),
                price real constraint ck_price check (price >= 0),
                discount real,
                constraint ck_discount check (discount is null or discount < price)
            )
            """);

        Assert.Collection(
            checks,
            check => { Assert.Equal(string.Empty, check.ConstraintName); Assert.Equal("quantity > 0", check.Definition); },
            check => { Assert.Equal("ck_price", check.ConstraintName); Assert.Equal("price >= 0", check.Definition); },
            check => { Assert.Equal("ck_discount", check.ConstraintName); Assert.Equal("discount is null or discount < price", check.Definition); });
    }

    [Theory]
    [InlineData("CONSTRAINT \"ck qty\" CHECK (qty > 0)", "ck qty")]
    [InlineData("CONSTRAINT [ck_qty] CHECK (qty > 0)", "ck_qty")]
    [InlineData("CONSTRAINT `ck_qty` CHECK (qty > 0)", "ck_qty")]
    public void Parse_RemovesNameDelimiters(string constraint, string expectedName)
    {
        var check = Assert.Single(SqLiteCheckConstraintParser.Parse($"CREATE TABLE t (qty integer, {constraint})"));

        Assert.Equal(expectedName, check.ConstraintName);
    }

    [Fact]
    public void Parse_KeepsNestedParenthesesAndOriginalSpacing()
    {
        var check = Assert.Single(SqLiteCheckConstraintParser.Parse(
            "create table if not exists main.t (status text check (status in ('open', 'closed (final)')  and  length(status) > 0))"));

        Assert.Equal("status in ('open', 'closed (final)')  and  length(status) > 0", check.Definition);
    }

    [Theory]
    [InlineData("CREATE TABLE t (id integer primary key, name text default 'check')")]
    [InlineData("")]
    [InlineData("not a create table statement")]
    public void Parse_ReturnsNothingWithoutChecks(string sql)
    {
        Assert.Empty(SqLiteCheckConstraintParser.Parse(sql));
    }
}
