using System.Data;
using DataDeveloper.Data.Services.Metadata;
using Xunit;

namespace DataDeveloper.Tests;

public class DdlResultReaderTests
{
    [Fact]
    public async Task ReadAsync_JoinsFirstColumnOfEveryRowAndResultSet()
    {
        var tableDdl = CreateTable(("Definition", typeof(string)), ["create table orders (id int);"]);
        var indexDdl = CreateTable(("Definition", typeof(string)), ["create index ix_a on orders (a);"], ["create index ix_b on orders (b);"]);

        var ddl = await ReadAsync(tableDdl, indexDdl);

        var separator = Environment.NewLine + Environment.NewLine;
        Assert.Equal($"create table orders (id int);{separator}create index ix_a on orders (a);{separator}create index ix_b on orders (b);", ddl);
    }

    [Fact]
    public async Task ReadAsync_SkipsNullAndBlankRows()
    {
        var table = CreateTable(("Definition", typeof(string)), [DBNull.Value], ["  "], ["create view v as select 1;"]);

        Assert.Equal("create view v as select 1;", await ReadAsync(table));
    }

    [Fact]
    public async Task ReadAsync_UsesMySqlCreateColumnInsteadOfObjectName()
    {
        var table = CreateTable(
            [("Table", typeof(string)), ("Create Table", typeof(string))],
            ["orders", "CREATE TABLE `orders` (`id` int)"]);

        Assert.Equal("CREATE TABLE `orders` (`id` int)", await ReadAsync(table));
    }

    [Fact]
    public async Task ReadAsync_ReturnsEmptyWhenMySqlHidesTheRoutineBody()
    {
        // SHOW CREATE PROCEDURE returns NULL in "Create Procedure" when the user lacks privileges;
        // the routine name in the first column must not be mistaken for its DDL.
        var table = CreateTable(
            [("Procedure", typeof(string)), ("sql_mode", typeof(string)), ("Create Procedure", typeof(string))],
            ["mark_order_shipped", "STRICT_TRANS_TABLES", DBNull.Value]);

        Assert.Equal(string.Empty, await ReadAsync(table));
    }

    private static async Task<string> ReadAsync(params DataTable[] tables)
    {
        using var dataSet = new DataSet();
        dataSet.Tables.AddRange(tables);
        await using var reader = dataSet.CreateDataReader();
        return await DdlResultReader.ReadAsync(reader);
    }

    private static DataTable CreateTable((string Name, Type Type) column, params object[][] rows)
    {
        return CreateTable([column], rows);
    }

    private static DataTable CreateTable((string Name, Type Type)[] columns, params object[][] rows)
    {
        var table = new DataTable();
        foreach (var (name, type) in columns)
            table.Columns.Add(name, type);
        foreach (var row in rows)
            table.Rows.Add(row);
        return table;
    }
}
