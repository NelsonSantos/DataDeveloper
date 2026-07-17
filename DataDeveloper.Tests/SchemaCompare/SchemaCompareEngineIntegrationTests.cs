using DataDeveloper.Data;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Models.SchemaCompare;
using DataDeveloper.Data.Providers.SqLite;
using DataDeveloper.Data.Services;
using DataDeveloper.Data.Services.SchemaCompare;
using DataDeveloper.Services.SchemaCompare;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DataDeveloper.Tests.SchemaCompare;

public class SchemaCompareEngineIntegrationTests
{
    [Fact]
    public async Task MissingTable_ProducesCreateScript_AndRoundTripsToUnchanged()
    {
        using var fixture = new TwoDatabaseFixture();
        fixture.ExecuteOnSource("create table customers (id integer primary key, name text not null);");

        var selected = new[] { new SchemaCompareObjectRef { ObjectType = SchemaCompareObjectType.Table, Name = "customers" } };
        var results = await SchemaCompareEngine.CompareAsync(fixture.Source, fixture.Destination, selected);

        var result = Assert.Single(results);
        Assert.Equal(SchemaCompareResultStatus.New, result.Status);
        Assert.NotNull(result.Script);
        Assert.Contains("create table", result.Script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("customers", result.Script, StringComparison.OrdinalIgnoreCase);

        fixture.ExecuteOnDestination(result.Script!);

        var resultsAfter = await SchemaCompareEngine.CompareAsync(fixture.Source, fixture.Destination, selected);
        var resultAfter = Assert.Single(resultsAfter);
        Assert.Equal(SchemaCompareResultStatus.Unchanged, resultAfter.Status);
    }

    [Fact]
    public async Task TableWithMissingColumn_ProducesAlterScript_AndRoundTripsToUnchanged()
    {
        using var fixture = new TwoDatabaseFixture();
        fixture.ExecuteOnSource("create table orders (id integer primary key, total real, discount real);");
        fixture.ExecuteOnDestination("create table orders (id integer primary key, total real);");

        var selected = new[] { new SchemaCompareObjectRef { ObjectType = SchemaCompareObjectType.Table, Name = "orders" } };
        var results = await SchemaCompareEngine.CompareAsync(fixture.Source, fixture.Destination, selected);

        var result = Assert.Single(results);
        Assert.Equal(SchemaCompareResultStatus.Changed, result.Status);
        Assert.NotNull(result.Script);
        Assert.Contains("add column", result.Script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("discount", result.Script, StringComparison.OrdinalIgnoreCase);

        fixture.ExecuteOnDestination(result.Script!);

        var resultsAfter = await SchemaCompareEngine.CompareAsync(fixture.Source, fixture.Destination, selected);
        var resultAfter = Assert.Single(resultsAfter);
        Assert.Equal(SchemaCompareResultStatus.Unchanged, resultAfter.Status);
    }

    [Fact]
    public async Task IdenticalTable_IsReportedAsUnchanged()
    {
        using var fixture = new TwoDatabaseFixture();
        const string ddl = "create table customers (id integer primary key, name text not null);";
        fixture.ExecuteOnSource(ddl);
        fixture.ExecuteOnDestination(ddl);

        var selected = new[] { new SchemaCompareObjectRef { ObjectType = SchemaCompareObjectType.Table, Name = "customers" } };
        var results = await SchemaCompareEngine.CompareAsync(fixture.Source, fixture.Destination, selected);

        var result = Assert.Single(results);
        Assert.Equal(SchemaCompareResultStatus.Unchanged, result.Status);
        Assert.Null(result.Script);

        var script = SchemaCompareEngine.BuildFinalScript("Source", "Destination", DatabaseType.SqLite, results);
        Assert.Contains("No changes selected", script);
    }

    [Fact]
    public async Task ChangedView_ProducesDropThenCreateScript()
    {
        using var fixture = new TwoDatabaseFixture();
        fixture.ExecuteOnSource(
            "create table orders (id integer primary key, total real);",
            "create view open_orders as select * from orders where total > 100;");
        fixture.ExecuteOnDestination(
            "create table orders (id integer primary key, total real);",
            "create view open_orders as select * from orders where total > 50;");

        var selected = new[] { new SchemaCompareObjectRef { ObjectType = SchemaCompareObjectType.View, Name = "open_orders" } };
        var results = await SchemaCompareEngine.CompareAsync(fixture.Source, fixture.Destination, selected);

        var result = Assert.Single(results);
        Assert.Equal(SchemaCompareResultStatus.Changed, result.Status);
        Assert.NotNull(result.Script);
        Assert.StartsWith("drop view if exists", result.Script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("create view", result.Script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("100", result.Script);
    }

    [Fact]
    public async Task ObjectOnlyOnDestination_IsReportedWithDropScript()
    {
        using var fixture = new TwoDatabaseFixture();
        fixture.ExecuteOnSource("create table customers (id integer primary key);");
        fixture.ExecuteOnDestination(
            "create table customers (id integer primary key);",
            "create table archive_log (id integer primary key);");

        var selected = new[] { new SchemaCompareObjectRef { ObjectType = SchemaCompareObjectType.Table, Name = "customers" } };
        var results = await SchemaCompareEngine.CompareAsync(fixture.Source, fixture.Destination, selected);

        var onlyInDestination = Assert.Single(results, r => r.Status == SchemaCompareResultStatus.OnlyInDestination);
        Assert.Equal("archive_log", onlyInDestination.Name);
        Assert.False(onlyInDestination.IsIncludedByDefault);
        Assert.NotNull(onlyInDestination.Script);
        Assert.Contains("drop table", onlyInDestination.Script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnselectedSourceObject_IsNotReportedAsOnlyInDestination()
    {
        using var fixture = new TwoDatabaseFixture();
        fixture.ExecuteOnSource(
            "create table customers (id integer primary key);",
            "create table orders (id integer primary key);");
        fixture.ExecuteOnDestination(
            "create table customers (id integer primary key);",
            "create table orders (id integer primary key);");

        // "orders" exists on both sides but is deliberately left unselected.
        var selected = new[] { new SchemaCompareObjectRef { ObjectType = SchemaCompareObjectType.Table, Name = "customers" } };
        var results = await SchemaCompareEngine.CompareAsync(fixture.Source, fixture.Destination, selected);

        Assert.DoesNotContain(results, r => r.Name.Equals("orders", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ThreeTableForeignKeyChain_GeneratesScriptThatRunsTopToBottomWithoutReordering()
    {
        using var fixture = new TwoDatabaseFixture();
        fixture.ExecuteOnSource(
            "create table categories (id integer primary key);",
            "create table products (id integer primary key, category_id integer references categories(id));",
            "create table order_items (id integer primary key, product_id integer references products(id));");

        var selected = new[]
        {
            new SchemaCompareObjectRef { ObjectType = SchemaCompareObjectType.Table, Name = "order_items" },
            new SchemaCompareObjectRef { ObjectType = SchemaCompareObjectType.Table, Name = "products" },
            new SchemaCompareObjectRef { ObjectType = SchemaCompareObjectType.Table, Name = "categories" }
        };

        var results = await SchemaCompareEngine.CompareAsync(fixture.Source, fixture.Destination, selected);
        Assert.All(results, r => Assert.Equal(SchemaCompareResultStatus.New, r.Status));

        var script = SchemaCompareEngine.BuildFinalScript("Source", "Destination", DatabaseType.SqLite, results);

        var categoriesIndex = script.IndexOf("-- ==== Table categories", StringComparison.OrdinalIgnoreCase);
        var productsIndex = script.IndexOf("-- ==== Table products", StringComparison.OrdinalIgnoreCase);
        var orderItemsIndex = script.IndexOf("-- ==== Table order_items", StringComparison.OrdinalIgnoreCase);

        Assert.True(categoriesIndex >= 0 && categoriesIndex < productsIndex);
        Assert.True(productsIndex < orderItemsIndex);

        // Executing the script top-to-bottom must succeed without manual reordering.
        fixture.ExecuteOnDestination(script);

        var resultsAfter = await SchemaCompareEngine.CompareAsync(fixture.Source, fixture.Destination, selected);
        Assert.All(resultsAfter, r => Assert.Equal(SchemaCompareResultStatus.Unchanged, r.Status));
    }

    [Fact]
    public async Task CompareAsync_WithAlreadyCancelledToken_ThrowsWithoutProcessingAnything()
    {
        using var fixture = new TwoDatabaseFixture();
        fixture.ExecuteOnSource("create table t1 (id integer primary key);");

        var selected = new[] { new SchemaCompareObjectRef { ObjectType = SchemaCompareObjectType.Table, Name = "t1" } };

        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            SchemaCompareEngine.CompareAsync(fixture.Source, fixture.Destination, selected, cancellationToken: cancellationTokenSource.Token));
    }

    [Fact]
    public async Task CompareAsync_CancelledMidway_StopsBeforeProcessingRemainingObjects()
    {
        using var fixture = new TwoDatabaseFixture();
        fixture.ExecuteOnSource(
            "create table t1 (id integer primary key);",
            "create table t2 (id integer primary key);",
            "create table t3 (id integer primary key);");

        var selected = new[]
        {
            new SchemaCompareObjectRef { ObjectType = SchemaCompareObjectType.Table, Name = "t1" },
            new SchemaCompareObjectRef { ObjectType = SchemaCompareObjectType.Table, Name = "t2" },
            new SchemaCompareObjectRef { ObjectType = SchemaCompareObjectType.Table, Name = "t3" }
        };

        using var cancellationTokenSource = new CancellationTokenSource();
        var reportedCount = 0;
        // A synchronous IProgress<T> (not the default Progress<T>, which dispatches via
        // SynchronizationContext.Post and would race with the loop's next iteration) so the
        // cancellation below is guaranteed to be observed before a second object is processed.
        var progress = new SynchronousProgress<(int Completed, int Total, string CurrentObjectName)>(_ =>
        {
            reportedCount++;
            if (reportedCount == 1)
                cancellationTokenSource.Cancel();
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            SchemaCompareEngine.CompareAsync(fixture.Source, fixture.Destination, selected, progress, cancellationTokenSource.Token));

        Assert.Equal(1, reportedCount);
    }

    [Fact]
    public async Task ExecuteScriptAsync_AppliesGeneratedScript_AndRoundTripsToUnchanged()
    {
        using var fixture = new TwoDatabaseFixture();
        fixture.ExecuteOnSource("create table customers (id integer primary key, name text not null);");

        var selected = new[] { new SchemaCompareObjectRef { ObjectType = SchemaCompareObjectType.Table, Name = "customers" } };
        var results = await SchemaCompareEngine.CompareAsync(fixture.Source, fixture.Destination, selected);
        var script = SchemaCompareEngine.BuildFinalScript("Source", "Destination", DatabaseType.SqLite, results);

        await SchemaCompareEngine.ExecuteScriptAsync(fixture.Destination, script);

        var resultsAfter = await SchemaCompareEngine.CompareAsync(fixture.Source, fixture.Destination, selected);
        var resultAfter = Assert.Single(resultsAfter);
        Assert.Equal(SchemaCompareResultStatus.Unchanged, resultAfter.Status);
    }

    [Fact]
    public async Task ExecuteScriptAsync_OnFailure_RollsBackWithNoPartialChanges()
    {
        using var fixture = new TwoDatabaseFixture();

        // The second statement fails (duplicate table) - since SQLite supports transactional
        // DDL, the first statement must be rolled back too, leaving nothing applied.
        const string script = "create table t1 (id integer primary key); create table t1 (id integer primary key);";

        await Assert.ThrowsAnyAsync<Exception>(() => SchemaCompareEngine.ExecuteScriptAsync(fixture.Destination, script));

        var destinationObjects = await SchemaCompareObjectEnumerator.EnumerateAsync(fixture.Destination);
        Assert.DoesNotContain(destinationObjects, o => o.Name.Equals("t1", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class SynchronousProgress<T>(Action<T> callback) : IProgress<T>
    {
        public void Report(T value) => callback(value);
    }

    private sealed class TwoDatabaseFixture : IDisposable
    {
        private readonly string _sourcePath;
        private readonly string _destinationPath;

        public TwoDatabaseFixture()
        {
            var services = new ServiceCollection();
            services.AddSingleton<DatabaseProviderFactoryService>();
            DatabaseExtensionsMethods.SetServiceProvider(services.BuildServiceProvider());

            _sourcePath = Path.Combine(Path.GetTempPath(), $"datadeveloper-schemacompare-source-{Guid.NewGuid():N}.db");
            _destinationPath = Path.Combine(Path.GetTempPath(), $"datadeveloper-schemacompare-destination-{Guid.NewGuid():N}.db");

            Source = CreateConnectionSettings(_sourcePath);
            Destination = CreateConnectionSettings(_destinationPath);
        }

        public SqLiteConnectionSettings Source { get; }
        public SqLiteConnectionSettings Destination { get; }

        public void ExecuteOnSource(params string[] statements) => Execute(_sourcePath, statements);
        public void ExecuteOnDestination(params string[] statements) => Execute(_destinationPath, statements);

        private static void Execute(string databasePath, string[] statements)
        {
            using var connection = new SqliteConnection($"Data Source={databasePath}");
            connection.Open();
            foreach (var statement in statements)
            {
                using var command = connection.CreateCommand();
                command.CommandText = statement;
                command.ExecuteNonQuery();
            }
        }

        private static SqLiteConnectionSettings CreateConnectionSettings(string databasePath)
        {
            return new SqLiteConnectionSettings
            {
                Id = Guid.NewGuid(),
                Name = Path.GetFileNameWithoutExtension(databasePath),
                DatabaseType = DatabaseType.SqLite,
                Database = databasePath
            };
        }

        public void Dispose()
        {
            if (File.Exists(_sourcePath))
                File.Delete(_sourcePath);
            if (File.Exists(_destinationPath))
                File.Delete(_destinationPath);
        }
    }
}
