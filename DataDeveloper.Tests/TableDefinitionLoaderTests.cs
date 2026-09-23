using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Models.TableDesigner;
using DataDeveloper.Data.Services.TableDesigner;
using Xunit;

namespace DataDeveloper.Tests;

public class TableDefinitionLoaderTests
{
    [Fact]
    public void Build_OracleMixedCaseNames_AreHeldSoTheGeneratedDdlTargetsTheSameObjects()
    {
        var definition = TableDefinitionLoader.Build(
            DatabaseType.Oracle,
            "DATADEVELOPER",
            "MinhaTabela",
            [
                new ColumnModel { Name = "Id", DataType = "number", IsPrimaryKey = true },
                new ColumnModel { Name = "PAIS_ID", DataType = "number" }
            ],
            new TableStructure
            {
                PrimaryKeyColumns = [new PrimaryKeyColumnModel { ConstraintName = "PK_Minha", ColumnName = "Id", OrdinalPosition = 1 }],
                ForeignKeyColumns =
                [
                    new ForeignKeyColumnModel
                    {
                        ConstraintName = "FK_MINHA_PAIS", ColumnName = "PAIS_ID", OrdinalPosition = 1,
                        ReferencedSchemaName = "DATADEVELOPER", ReferencedTableName = "Pais", ReferencedColumnName = "Codigo"
                    }
                ],
                IndexColumns = [new IndexColumnModel { IndexName = "IX_MINHA_PAIS", ColumnName = "PAIS_ID", OrdinalPosition = 1 }]
            });

        Assert.Equal("DATADEVELOPER", definition.SchemaName);
        Assert.Equal("\"MinhaTabela\"", definition.TableName);
        Assert.Equal(["\"Id\"", "PAIS_ID"], definition.Columns.Select(column => column.Name));
        Assert.Equal("\"Id\"", definition.Columns[0].OriginalName);
        Assert.Equal("\"PK_Minha\"", definition.PrimaryKey.Name);
        Assert.Equal("\"Id\"", Assert.Single(definition.PrimaryKey.ColumnNames));
        var foreignKey = Assert.Single(definition.ForeignKeys);
        Assert.Equal("\"Pais\"", foreignKey.ReferencedTableName);
        Assert.Equal("\"Codigo\"", Assert.Single(foreignKey.ReferencedColumnNames));

        var edited = Clone(definition);
        edited.Columns.Add(new TableColumnDefinition { Name = "observacao", DataType = "varchar2", Length = 50, IsNullable = true });

        var script = TableDdlScriptBuilder.BuildAlterTableScript(DatabaseType.Oracle, definition, edited);

        // The existing table keeps its exact name; a name typed in the designer follows Oracle's upper-casing.
        Assert.Contains("alter table DATADEVELOPER.\"MinhaTabela\" add OBSERVACAO varchar2(50)", script, StringComparison.Ordinal);
        Assert.DoesNotContain("MINHATABELA", script, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(DatabaseType.SqlServer)]
    [InlineData(DatabaseType.PostgresSql)]
    [InlineData(DatabaseType.MySql)]
    [InlineData(DatabaseType.SqLite)]
    public void Build_KeepsStoredNamesOutsideOracle(DatabaseType databaseType)
    {
        var definition = TableDefinitionLoader.Build(
            databaseType,
            string.Empty,
            "MinhaTabela",
            [new ColumnModel { Name = "Id", DataType = "int", IsPrimaryKey = true }],
            new TableStructure());

        Assert.Equal("MinhaTabela", definition.TableName);
        Assert.Equal("Id", Assert.Single(definition.Columns).Name);
    }

    private static TableDefinition Clone(TableDefinition definition)
    {
        var clone = new TableDefinition { SchemaName = definition.SchemaName, TableName = definition.TableName };
        foreach (var column in definition.Columns)
        {
            clone.Columns.Add(new TableColumnDefinition
            {
                OriginalName = column.OriginalName, Name = column.Name, DataType = column.DataType, Length = column.Length,
                Precision = column.Precision, Scale = column.Scale, IsNullable = column.IsNullable, IsIdentity = column.IsIdentity,
                DefaultValue = column.DefaultValue
            });
        }

        clone.PrimaryKey.Name = definition.PrimaryKey.Name;
        clone.PrimaryKey.ColumnNames.AddRange(definition.PrimaryKey.ColumnNames);
        clone.ForeignKeys.AddRange(definition.ForeignKeys);
        clone.Indexes.AddRange(definition.Indexes);
        return clone;
    }
}
