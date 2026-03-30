using System.Data;
using DataDeveloper.Data.Services;
using Xunit;

namespace DataDeveloper.Tests;

public class ResultSetMaterializerTests
{
    [Fact]
    public async Task MaterializeCurrentResult_PreservesNextResultSet()
    {
        var firstTable = new DataTable();
        firstTable.Columns.Add("Id", typeof(int));
        firstTable.Rows.Add(1);

        var secondTable = new DataTable();
        secondTable.Columns.Add("Name", typeof(string));
        secondTable.Rows.Add("abc");

        var dataSet = new DataSet();
        dataSet.Tables.Add(firstTable);
        dataSet.Tables.Add(secondTable);

        await using var reader = dataSet.CreateDataReader();

        var materializedFirst = ResultSetMaterializer.MaterializeCurrentResult(reader);

        Assert.Single(materializedFirst.Rows);
        Assert.Equal(1, materializedFirst.Rows[0]["Id"]);

        Assert.True(await reader.NextResultAsync());

        var materializedSecond = ResultSetMaterializer.MaterializeCurrentResult(reader);

        Assert.Single(materializedSecond.Rows);
        Assert.Equal("abc", materializedSecond.Rows[0]["Name"]);
    }

    [Fact]
    public void DataTableReader_HasRows_IsTrue_WhenTableHasRows()
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(int));
        table.Rows.Add(1);

        using var reader = table.CreateDataReader();

        Assert.True(reader.HasRows);
    }

    [Fact]
    public void DataTableReader_PreservesSchema_WhenTableHasNoRows()
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(int));

        using var reader = table.CreateDataReader();

        Assert.Equal(1, reader.FieldCount);
        Assert.False(reader.HasRows);
    }
}
