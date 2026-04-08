using DataDeveloper.Data.Models;
using DataDeveloper.Data.Services;
using Xunit;

namespace DataDeveloper.Tests;

public class ResultSetEditabilityAnalyzerTests
{
    [Theory]
    [InlineData("select * from clientes", "clientes")]
    [InlineData(" select * from dbo.clientes ", "dbo.clientes")]
    [InlineData("select * from [dbo].[clientes];", "[dbo].[clientes]")]
    [InlineData("select * from `clientes`", "`clientes`")]
    [InlineData("/* comment */ select * from \"clientes\"", "\"clientes\"")]
    [InlineData("select * from clientes c", "clientes")]
    [InlineData("select * from clientes as c where c.id = 1", "clientes")]
    [InlineData("select * from dbo.clientes c where c.id = 1 order by c.nome", "dbo.clientes")]
    [InlineData("select c.* from clientes c", "clientes")]
    [InlineData("select c.* from dbo.clientes c where c.id = 1", "dbo.clientes")]
    [InlineData("select c.* from [dbo].[clientes] as c where c.id = 1 order by c.nome", "[dbo].[clientes]")]
    public void Analyze_ReturnsEditable_ForSimpleSelectStarStatements(string statement, string expectedTable)
    {
        var result = ResultSetEditabilityAnalyzer.Analyze(statement);

        Assert.True(result.IsEditable);
        Assert.Equal(expectedTable, result.TableName);
        Assert.Null(result.Reason);
    }

    [Theory]
    [InlineData("select id, nome from clientes")]
    [InlineData("select * from clientes join pedidos on pedidos.cliente_id = clientes.id")]
    [InlineData("select top 10 * from clientes")]
    [InlineData("select\n*\nfrom clientes join pedidos on pedidos.cliente_id = clientes.id")]
    [InlineData("select c.id, c.nome from clientes c where c.id = 1")]
    [InlineData("select distinct * from clientes")]
    [InlineData("select x.* from clientes c where c.id = 1")]
    public void Analyze_ReturnsNotEditable_ForUnsupportedSelectShapes(string statement)
    {
        var result = ResultSetEditabilityAnalyzer.Analyze(statement);

        Assert.False(result.IsEditable);
        Assert.Null(result.TableName);
        Assert.NotNull(result.Reason);
    }

    [Fact]
    public void Analyze_ReturnsNotEditable_WhenTargetTableHasNoPrimaryKey()
    {
        var result = ResultSetEditabilityAnalyzer.Analyze(
            "select * from clientes",
            ["id", "nome"],
            [new ColumnModel { Name = "id" }, new ColumnModel { Name = "nome" }]);

        Assert.False(result.IsEditable);
        Assert.Equal("clientes", result.TableName);
        Assert.Equal("The target table does not have a primary key.", result.Reason);
    }

    [Fact]
    public void Analyze_ReturnsNotEditable_WhenPrimaryKeyColumnIsMissingFromResult()
    {
        var result = ResultSetEditabilityAnalyzer.Analyze(
            "select * from clientes",
            ["nome"],
            [new ColumnModel { Name = "id", IsPrimaryKey = true }, new ColumnModel { Name = "nome" }]);

        Assert.False(result.IsEditable);
        Assert.Equal("clientes", result.TableName);
        Assert.Equal("Primary key columns are missing from the result: id.", result.Reason);
    }

    [Fact]
    public void Analyze_ReturnsEditable_WhenPrimaryKeyColumnsAreVisible()
    {
        var result = ResultSetEditabilityAnalyzer.Analyze(
            "select * from clientes",
            ["id", "codigo", "nome"],
            [
                new ColumnModel { Name = "id", IsPrimaryKey = true },
                new ColumnModel { Name = "codigo", IsPrimaryKey = true },
                new ColumnModel { Name = "nome" }
            ]);

        Assert.True(result.IsEditable);
        Assert.Equal("clientes", result.TableName);
        Assert.Null(result.Reason);
    }
}
