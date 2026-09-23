using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Services.SqlDialects;
using Xunit;

namespace DataDeveloper.Tests;

public class DbObjectRefTests
{
    [Theory]
    [InlineData("orders", null, "orders")]
    [InlineData("dbo.Orders", "dbo", "Orders")]
    [InlineData("[dbo].[Order.Items]", "dbo", "Order.Items")]
    [InlineData("db.sales.orders", "sales", "orders")]
    [InlineData(" public . orders ", "public", "orders")]
    public void Parse_KeepsLastTwoPartsWithoutDelimiters(string qualifiedName, string? expectedSchema, string expectedName)
    {
        var databaseObject = DbObjectRef.Parse(DbObjectKind.Table, qualifiedName, SqlDialect.For(DatabaseType.SqlServer));

        Assert.Equal(expectedSchema, databaseObject.Schema);
        Assert.Equal(expectedName, databaseObject.Name);
    }

    [Theory]
    [InlineData("orders", "orders")]
    [InlineData("sales.orders", "sales.orders")]
    public void QualifiedName_JoinsSchemaAndName(string qualifiedName, string expected)
    {
        var databaseObject = DbObjectRef.Parse(DbObjectKind.View, qualifiedName, SqlDialect.For(DatabaseType.PostgresSql));

        Assert.Equal(expected, databaseObject.QualifiedName);
    }

    [Theory]
    [InlineData(NodeType.Table, DbObjectKind.Table)]
    [InlineData(NodeType.View, DbObjectKind.View)]
    [InlineData(NodeType.Procedure, DbObjectKind.Procedure)]
    [InlineData(NodeType.Function, DbObjectKind.Function)]
    public void FromSchemaNode_MapsObjectNodes(NodeType nodeType, DbObjectKind expectedKind)
    {
        var node = CreateNode(nodeType, "sales.orders");

        var databaseObject = DbObjectRef.FromSchemaNode(node, SqlDialect.For(DatabaseType.MySql));

        Assert.NotNull(databaseObject);
        Assert.Equal(new DbObjectRef(expectedKind, "sales", "orders"), databaseObject);
    }

    [Theory]
    [InlineData(NodeType.Tables)]
    [InlineData(NodeType.Column)]
    [InlineData(NodeType.Parameter)]
    [InlineData(NodeType.Connection)]
    public void FromSchemaNode_ReturnsNullForNonObjectNodes(NodeType nodeType)
    {
        var node = CreateNode(nodeType, "anything");

        Assert.Null(DbObjectRef.FromSchemaNode(node, SqlDialect.For(DatabaseType.MySql)));
    }

    private static SchemaNode CreateNode(NodeType nodeType, string name)
    {
        return (SchemaNode)Activator.CreateInstance(
                   typeof(SchemaNode),
                   System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                   binder: null,
                   args: [nodeType, name, false, null, false, null, null],
                   culture: null)!;
    }
}
