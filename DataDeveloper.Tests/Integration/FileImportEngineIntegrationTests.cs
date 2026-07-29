using System.Linq;
using System.Text;
using DataDeveloper.Data;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Models.FileImport;
using DataDeveloper.Data.Models.TableDesigner;
using DataDeveloper.Data.Services.FileImport;
using Xunit;

namespace DataDeveloper.Tests.Integration;

public class FileImportEngineIntegrationTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    public FileImportEngineIntegrationTests()
    {
        DatabaseIntegrationTestSupport.EnsureDatabaseServices();
    }

    public void Dispose()
    {
        foreach (var path in _tempFiles.Where(File.Exists))
            File.Delete(path);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SqLite_NewTable_CreatesTableAndImportsAllRows()
    {
        if (!DatabaseIntegrationTestSupport.ShouldRunIntegrationTests())
            return;

        var connectionSettings = await DatabaseIntegrationTestSupport.CreateSqLiteConnectionAsync();
        var filePath = CreateCsvFile("id,name\n1,Alice\n2,Bob\n3,Carol\n");

        var table = new TableDefinition { TableName = "imported_people" };
        table.Columns.Add(new TableColumnDefinition { Name = "id", DataType = "integer", IsNullable = false });
        table.Columns.Add(new TableColumnDefinition { Name = "name", DataType = "text", IsNullable = true });

        var mappings = new List<FileImportColumnMapping>
        {
            new(0, "id") { NewColumn = table.Columns[0] },
            new(1, "name") { NewColumn = table.Columns[1] }
        };

        await FileImportEngine.CreateTableAsync(connectionSettings, table);
        var columnModels = FileImportEngine.BuildColumnModels(table);

        var result = await FileImportEngine.ImportRowsAsync(connectionSettings, table.TableName, columnModels, filePath, mappings);

        Assert.Equal(3, result.RowsImported);
        Assert.Equal(0, result.RowsFailed);
        Assert.Empty(result.Errors);

        var rowCount = await DatabaseIntegrationTestSupport.ExecuteScalarIntAsync(connectionSettings, "select count(*) from imported_people");
        Assert.Equal(3, rowCount);

        var snapshot = await DatabaseIntegrationTestSupport.ExecuteQuerySnapshotAsync(connectionSettings, "select id, name from imported_people order by id");
        Assert.Equal(3, snapshot.Rows.Count);
        Assert.Equal("Alice", snapshot.Rows[0][1]);
        Assert.Equal("Carol", snapshot.Rows[2][1]);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SqLite_ExistingTable_ImportsMappedRowsIntoSeededTable()
    {
        if (!DatabaseIntegrationTestSupport.ShouldRunIntegrationTests())
            return;

        var connectionSettings = await DatabaseIntegrationTestSupport.CreateSqLiteConnectionAsync();
        var filePath = CreateCsvFile("full_name,email\nDave Miller,dave@example.com\nEve Adams,eve@example.com\n");

        var explorer = connectionSettings.GetSchemaExplorer();
        await explorer.InitializeSchemaNode();
        var tablesFolder = explorer.RootConnections.First().Children.First(child => child.NodeType == NodeType.Tables);
        var customersNode = tablesFolder.Children.First(child => child.Name.Contains("customers", StringComparison.OrdinalIgnoreCase));
        var columnsFolder = customersNode.Children.First(child => child.NodeType == NodeType.Columns);
        await explorer.LoadNodeAsync(columnsFolder);
        var columnModels = columnsFolder.Children
            .Where(child => child.NodeType == NodeType.Column && child.Tag is ColumnModel)
            .Select(child => (ColumnModel)child.Tag!)
            .ToList();

        var mappings = new List<FileImportColumnMapping>
        {
            new(0, "full_name") { TargetColumnName = "name" },
            new(1, "email") { TargetColumnName = "email" }
        };

        var result = await FileImportEngine.ImportRowsAsync(connectionSettings, customersNode.Name, columnModels, filePath, mappings);

        Assert.Equal(2, result.RowsImported);
        Assert.Equal(0, result.RowsFailed);

        // the seed data already has 2 customers; the import adds 2 more.
        var rowCount = await DatabaseIntegrationTestSupport.ExecuteScalarIntAsync(connectionSettings, "select count(*) from customers");
        Assert.Equal(4, rowCount);

        var addedRowCount = await DatabaseIntegrationTestSupport.ExecuteScalarIntAsync(
            connectionSettings, "select count(*) from customers where email = 'dave@example.com'");
        Assert.Equal(1, addedRowCount);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SqLite_RowViolatingPrimaryKeyUniqueness_IsRolledBackAndReportedAsFailed()
    {
        if (!DatabaseIntegrationTestSupport.ShouldRunIntegrationTests())
            return;

        var connectionSettings = await DatabaseIntegrationTestSupport.CreateSqLiteConnectionAsync();
        // Row 2 reuses id=1, which violates the primary key's uniqueness once row 1 is inserted.
        var filePath = CreateCsvFile("id,name\n1,Alice\n1,AliceDuplicate\n3,Carol\n");

        var table = new TableDefinition { TableName = "strict_people" };
        table.Columns.Add(new TableColumnDefinition { Name = "id", DataType = "integer", IsNullable = false });
        table.Columns.Add(new TableColumnDefinition { Name = "name", DataType = "text", IsNullable = true });
        table.PrimaryKey.ColumnNames.Add("id");

        var mappings = new List<FileImportColumnMapping>
        {
            new(0, "id") { NewColumn = table.Columns[0] },
            new(1, "name") { NewColumn = table.Columns[1] }
        };

        await FileImportEngine.CreateTableAsync(connectionSettings, table);
        var columnModels = FileImportEngine.BuildColumnModels(table);

        var result = await FileImportEngine.ImportRowsAsync(connectionSettings, table.TableName, columnModels, filePath, mappings);

        Assert.Equal(1, result.RowsFailed);
        Assert.Single(result.Errors);

        // Row 1 was rolled back together with row 2's failed insert (same in-flight transaction);
        // only row 3, imported in a fresh transaction after the rollback, made it to the table.
        Assert.Equal(1, result.RowsImported);
        var rowCount = await DatabaseIntegrationTestSupport.ExecuteScalarIntAsync(connectionSettings, "select count(*) from strict_people");
        Assert.Equal(1, rowCount);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SqLite_BlankAndLiteralNullCells_AreImportedAsActualNulls()
    {
        if (!DatabaseIntegrationTestSupport.ShouldRunIntegrationTests())
            return;

        var connectionSettings = await DatabaseIntegrationTestSupport.CreateSqLiteConnectionAsync();
        // Row 2's note is blank and row 3's is the literal text "NULL" - both common null
        // markers produced by spreadsheet/CSV exports, and both must become a real NULL rather
        // than the four-character string "NULL" being inserted.
        var filePath = CreateCsvFile("id,note\n1,hello\n2,\n3,NULL\n");

        var table = new TableDefinition { TableName = "notes_table" };
        table.Columns.Add(new TableColumnDefinition { Name = "id", DataType = "integer", IsNullable = false });
        table.Columns.Add(new TableColumnDefinition { Name = "note", DataType = "text", IsNullable = true });

        var mappings = new List<FileImportColumnMapping>
        {
            new(0, "id") { NewColumn = table.Columns[0] },
            new(1, "note") { NewColumn = table.Columns[1] }
        };

        await FileImportEngine.CreateTableAsync(connectionSettings, table);
        var columnModels = FileImportEngine.BuildColumnModels(table);

        var result = await FileImportEngine.ImportRowsAsync(connectionSettings, table.TableName, columnModels, filePath, mappings);

        Assert.Equal(3, result.RowsImported);
        Assert.Equal(0, result.RowsFailed);

        var snapshot = await DatabaseIntegrationTestSupport.ExecuteQuerySnapshotAsync(connectionSettings, "select id, note from notes_table order by id");
        Assert.Equal("hello", snapshot.Rows[0][1]);
        Assert.Equal(DBNull.Value, snapshot.Rows[1][1]);
        Assert.Equal(DBNull.Value, snapshot.Rows[2][1]);
    }

    private string CreateCsvFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.csv");
        File.WriteAllText(path, content, Encoding.UTF8);
        _tempFiles.Add(path);
        return path;
    }
}
