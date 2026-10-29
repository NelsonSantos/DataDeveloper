using DataDeveloper.Data.Ai;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Models;
using Xunit;

namespace DataDeveloper.Tests.Ai;

public class AiSqlContextBuilderTests
{
    private static DbObjectRef Table(string name, string? schema = null) => new(DbObjectKind.Table, schema, name);

    private static FakeSource CreateSource() => new(
        [Table("Customers", "dbo"), Table("Orders", "dbo"), Table("Audit", "dbo"), new DbObjectRef(DbObjectKind.View, "dbo", "ActiveCustomers")])
    {
        Columns =
        {
            ["Orders"] = [new ColumnModel { Name = "Id", DataType = "int", IsPrimaryKey = true }, new ColumnModel { Name = "CustomerId", DataType = "int" }, new ColumnModel { Name = "Note", DataType = "varchar", Length = 50, IsNullable = true }],
            ["Customers"] = [new ColumnModel { Name = "Id", DataType = "int", IsPrimaryKey = true }]
        },
        Keys =
        {
            ["Orders"] = new TableKeys
            {
                ForeignKeyColumns = [new ForeignKeyColumnModel { ConstraintName = "FK_Orders_Customers", ColumnName = "CustomerId", ReferencedSchemaName = "dbo", ReferencedTableName = "Customers", ReferencedColumnName = "Id" }]
            }
        }
    };

    [Fact]
    public async Task Prompt_NamesTheDialectAndListsObjects()
    {
        var builder = new AiSqlContextBuilder(CreateSource(), DatabaseType.PostgresSql);

        var prompt = await builder.BuildSystemPromptAsync("hello", []);

        Assert.Contains("PostgreSQL", prompt);
        Assert.Contains("dbo.Customers", prompt);
        Assert.Contains("dbo.ActiveCustomers", prompt);
        Assert.DoesNotContain("Table dbo.Orders:", prompt);
    }

    [Theory]
    [InlineData(DatabaseType.SqlServer)]
    [InlineData(DatabaseType.PostgresSql)]
    [InlineData(DatabaseType.Oracle)]
    [InlineData(DatabaseType.MySql)]
    [InlineData(DatabaseType.SqLite)]
    public async Task StablePrompt_TellsTheModelToAskForTablesInsteadOfGuessingColumns(DatabaseType databaseType)
    {
        var builder = new AiSqlContextBuilder(CreateSource(), databaseType);

        var prompt = await builder.BuildStableSystemPromptAsync();

        Assert.Contains("do not guess them", prompt);
        Assert.Contains("Tables / Views", prompt);
    }

    [Fact]
    public async Task Prompt_DescribesObjectsMentionedInTheText()
    {
        var builder = new AiSqlContextBuilder(CreateSource(), DatabaseType.SqlServer);

        var prompt = await builder.BuildSystemPromptAsync("select * from orders o join customers c on c.id = o.customerid", []);

        Assert.Contains("Table dbo.Orders:", prompt);
        Assert.Contains("CustomerId int", prompt);
        Assert.Contains("Note varchar(50)", prompt);
        Assert.Contains("foreign key (CustomerId) -> dbo.Customers(Id)", prompt);
        Assert.Contains("Table dbo.Customers:", prompt);
        Assert.DoesNotContain("Table dbo.Audit:", prompt);
    }

    [Fact]
    public async Task Prompt_AlwaysDescribesExplicitlyAttachedObjects()
    {
        var builder = new AiSqlContextBuilder(CreateSource(), DatabaseType.Oracle);

        var prompt = await builder.BuildSystemPromptAsync("nothing relevant", [Table("Orders")]);

        Assert.Contains("Table dbo.Orders:", prompt);
    }

    [Fact]
    public async Task Prompt_SurvivesAFailingCatalog()
    {
        var builder = new AiSqlContextBuilder(new FakeSource([]) { ListFails = true }, DatabaseType.MySql);

        var prompt = await builder.BuildSystemPromptAsync("x", []);

        Assert.Contains("could not be read", prompt);
    }

    [Fact]
    public async Task Objects_AreCachedUntilInvalidated()
    {
        var source = CreateSource();
        var builder = new AiSqlContextBuilder(source, DatabaseType.SqLite);

        await builder.GetObjectsAsync();
        await builder.GetObjectsAsync();
        builder.Invalidate();
        await builder.GetObjectsAsync();

        Assert.Equal(2, source.ListCalls);
    }

    [Fact]
    public void SelectDetailed_CapsTheNumberOfDescribedObjects()
    {
        var objects = Enumerable.Range(1, 40).Select(i => Table($"Table{i}")).ToList();
        var text = string.Join(' ', objects.Select(o => o.Name));

        Assert.Equal(AiSqlContextBuilder.MaxDetailedObjects, AiSqlContextBuilder.SelectDetailed(objects, text, []).Count);
    }

    private sealed class FakeSource(IReadOnlyList<DbObjectRef> objects) : IAiSchemaSource
    {
        public Dictionary<string, IReadOnlyList<ColumnModel>> Columns { get; } = new();
        public Dictionary<string, TableKeys> Keys { get; } = new();
        public bool ListFails { get; init; }
        public int ListCalls { get; private set; }

        public Task<IReadOnlyList<DbObjectRef>> ListTablesAndViewsAsync(CancellationToken cancellationToken = default)
        {
            ListCalls++;
            return ListFails ? throw new InvalidOperationException("no access") : Task.FromResult(objects);
        }

        public Task<IReadOnlyList<ColumnModel>> GetColumnsAsync(DbObjectRef table, CancellationToken cancellationToken = default) =>
            Task.FromResult(Columns.GetValueOrDefault(table.Name) ?? []);

        public Task<TableKeys> GetKeysAsync(DbObjectRef table, CancellationToken cancellationToken = default) =>
            Task.FromResult(Keys.GetValueOrDefault(table.Name) ?? new TableKeys());
    }
}
