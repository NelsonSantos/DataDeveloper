using DataDeveloper.Data.Enums;
using DataDeveloper.Services;
using Xunit;

namespace DataDeveloper.Tests;

public sealed class SqlTokenFormatterTests
{
    [Fact]
    public void Format_PostgresSql_BreaksMajorClauses()
    {
        var sql = "select id, name from customers where active = true order by name";

        var formatted = SqlTokenFormatter.Format(sql, DatabaseType.PostgresSql, "    ");

        Assert.Contains("SELECT", formatted, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\nFROM", formatted, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\nWHERE", formatted, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\nORDER BY", formatted, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Format_Oracle_PreservesCommentTokens()
    {
        var sql = "select id /* customer id */ from customers";

        var formatted = SqlTokenFormatter.Format(sql, DatabaseType.Oracle, "    ");

        Assert.Contains("/* customer id */", formatted);
        Assert.Contains("\nFROM", formatted, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Format_SqLite_BreaksInsertClauses()
    {
        var sql = "insert into customers(id, name) values(1, 'Alice')";

        var formatted = SqlTokenFormatter.Format(sql, DatabaseType.SqLite, "    ");

        Assert.Contains("INSERT INTO", formatted, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\nVALUES", formatted, StringComparison.OrdinalIgnoreCase);
    }
}
