using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Models.SchemaCompare;
using DataDeveloper.Data.Models.TableDesigner;
using DataDeveloper.Data.Services.SchemaCompare;
using Xunit;

namespace DataDeveloper.Tests.SchemaCompare;

public class NewTableDependencyOrdererTests
{
    [Fact]
    public void LinearChain_OrdersReferencedTableBeforeDependent()
    {
        var a = NewTable("A");
        var b = NewTable("B", ("A", "AId", "Id"));
        var c = NewTable("C", ("B", "BId", "Id"));

        var ordered = NewTableDependencyOrderer.Order(new[] { c, a, b });

        Assert.Equal(new[] { "A", "B", "C" }, ordered.Select(r => r.Name));
    }

    [Fact]
    public void Diamond_ReferencedTableFirstAndDependentLast()
    {
        var a = NewTable("A");
        var b = NewTable("B", ("A", "AId", "Id"));
        var c = NewTable("C", ("A", "AId", "Id"));
        var d = NewTable("D", ("B", "BId", "Id"), ("C", "CId", "Id"));

        var ordered = NewTableDependencyOrderer.Order(new[] { d, b, c, a });

        var names = ordered.Select(r => r.Name).ToList();
        Assert.Equal("A", names[0]);
        Assert.Equal("D", names[^1]);
        Assert.True(names.IndexOf("B") > names.IndexOf("A"));
        Assert.True(names.IndexOf("C") > names.IndexOf("A"));
    }

    [Fact]
    public void NoForeignKeys_PreservesOriginalOrder()
    {
        var a = NewTable("A");
        var b = NewTable("B");
        var c = NewTable("C");

        var ordered = NewTableDependencyOrderer.Order(new[] { c, a, b });

        Assert.Equal(new[] { "C", "A", "B" }, ordered.Select(r => r.Name));
    }

    [Fact]
    public void SelfReferencingForeignKey_DoesNotBlockTheTable()
    {
        var employees = NewTable("Employees", ("Employees", "ManagerId", "EmployeeId"));

        var ordered = NewTableDependencyOrderer.Order(new[] { employees });

        Assert.Equal(new[] { "Employees" }, ordered.Select(r => r.Name));
    }

    [Fact]
    public void ForeignKeyReferencingTableOutsideTheSet_DoesNotBlock()
    {
        var a = NewTable("A", ("PreExistingTable", "PreId", "Id"));

        var ordered = NewTableDependencyOrderer.Order(new[] { a });

        Assert.Equal(new[] { "A" }, ordered.Select(r => r.Name));
    }

    [Fact]
    public void Cycle_KeepsBothTablesAndAddsWarningComment()
    {
        var a = NewTable("A", ("B", "BId", "Id"));
        var b = NewTable("B", ("A", "AId", "Id"));

        var ordered = NewTableDependencyOrderer.Order(new[] { a, b });

        Assert.Equal(2, ordered.Count);
        Assert.Contains(ordered, r => r.Name == "A");
        Assert.Contains(ordered, r => r.Name == "B");
        Assert.Contains(ordered, r => r.Script != null && r.Script.Contains("WARNING: circular foreign key dependency"));
    }

    private static SchemaCompareObjectResult NewTable(string name, params (string ReferencedTable, string ColumnName, string ReferencedColumn)[] foreignKeys)
    {
        var definition = new TableDefinition { TableName = name };
        foreach (var fk in foreignKeys)
        {
            var foreignKey = new TableForeignKeyDefinition { ReferencedTableName = fk.ReferencedTable };
            foreignKey.ColumnNames.Add(fk.ColumnName);
            foreignKey.ReferencedColumnNames.Add(fk.ReferencedColumn);
            definition.ForeignKeys.Add(foreignKey);
        }

        return new SchemaCompareObjectResult
        {
            ObjectType = SchemaCompareObjectType.Table,
            Name = name,
            Status = SchemaCompareResultStatus.New,
            Script = $"create table {name} (...);",
            IsIncludedByDefault = true,
            NewTableDefinition = definition
        };
    }
}
