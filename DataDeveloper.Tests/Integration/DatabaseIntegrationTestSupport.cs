using System.Data.Common;
using System.Data;
using DataDeveloper.Data;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Providers.MySql;
using DataDeveloper.Data.Providers.Oracle;
using DataDeveloper.Data.Providers.PostgresSql;
using DataDeveloper.Data.Providers.SqLite;
using DataDeveloper.Data.Providers.SqlServer;
using DataDeveloper.Data.Services;
using DataDeveloper.Interfaces;
using DataDeveloper.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DataDeveloper.Tests.Integration;

internal static class DatabaseIntegrationTestSupport
{
    private const string IntegrationFlag = "RUN_DB_INTEGRATION_TESTS";

    public static bool ShouldRunIntegrationTests()
    {
        var value = Environment.GetEnvironmentVariable(IntegrationFlag);
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    public static void EnsureDatabaseServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<DatabaseProviderFactoryService>();
        services.AddSingleton<IEventAggregatorService, EventAggregatorService>();
        DatabaseExtensionsMethods.SetServiceProvider(services.BuildServiceProvider());
    }

    public static ConnectionSettings CreateConnectionSettings(DatabaseType databaseType)
    {
        return databaseType switch
        {
            DatabaseType.SqlServer => new SqlServerConnectionSettings
            {
                Name = "Integration SQL Server",
                DatabaseType = DatabaseType.SqlServer,
                Server = "localhost,14333",
                Database = "DataDeveloperIntegration",
                User = "sa",
                Password = "StrongPassw0rd!",
                Encrypt = false,
                TrustServerCertificate = true
            },
            DatabaseType.MySql => new MySqlConnectionSettings
            {
                Name = "Integration MySQL",
                DatabaseType = DatabaseType.MySql,
                Server = "localhost",
                Database = "datadeveloper",
                User = "datadeveloper",
                Password = "datadeveloper",
                Port = 3307,
                Encrypt = false,
                TrustServerCertificate = true
            },
            DatabaseType.PostgresSql => new PostgresConnectionSettings
            {
                Name = "Integration PostgreSQL",
                DatabaseType = DatabaseType.PostgresSql,
                Server = "localhost",
                Database = "datadeveloper",
                User = "datadeveloper",
                Password = "datadeveloper",
                Port = 5433,
                Encrypt = false,
                TrustServerCertificate = false
            },
            DatabaseType.Oracle => new OracleConnectionSettings
            {
                Name = "Integration Oracle",
                DatabaseType = DatabaseType.Oracle,
                Server = "localhost",
                Database = "FREEPDB1",
                User = "datadeveloper",
                Password = "datadeveloper",
                Port = 1522
            },
            _ => throw new NotSupportedException($"No integration connection configured for {databaseType}.")
        };
    }

    public static async Task<SqLiteConnectionSettings> CreateSqLiteConnectionAsync()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), "DataDeveloperIntegration", $"{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(Path.GetDirectoryName(tempFile)!);

        var connectionSettings = new SqLiteConnectionSettings
        {
            Name = "Integration SQLite",
            DatabaseType = DatabaseType.SqLite,
            Database = tempFile
        };

        await using var connection = new SqliteConnection($"Data Source={tempFile}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = GetSqLiteSeedSql();
        await command.ExecuteNonQueryAsync();

        return connectionSettings;
    }

    public static string GetSmokeTestQuery(DatabaseType databaseType)
    {
        return databaseType switch
        {
            DatabaseType.Oracle => "select 1 as value from dual",
            _ => "select 1 as value"
        };
    }

    public static async Task<T> WithTimeout<T>(Task<T> task, TimeSpan timeout, string operationName)
    {
        var completed = await Task.WhenAny(task, Task.Delay(timeout));
        if (completed != task)
            throw new TimeoutException($"{operationName} timed out after {timeout}.");

        return await task;
    }

    public static async Task WithTimeout(Task task, TimeSpan timeout, string operationName)
    {
        var completed = await Task.WhenAny(task, Task.Delay(timeout));
        if (completed != task)
            throw new TimeoutException($"{operationName} timed out after {timeout}.");

        await task;
    }

    public static async Task<IReadOnlyList<SchemaNode>> InitializeSchemaAsync(IConnectionSettings connectionSettings)
    {
        var explorer = connectionSettings.GetSchemaExplorer();
        await explorer.InitializeSchemaNode();
        return explorer.RootConnections.ToList();
    }

    public static async Task<IReadOnlyList<StatementResult>> ExecuteAsync(IConnectionSettings connectionSettings, string sql)
    {
        var executor = connectionSettings.GetStatementExecutor();
        return (await executor.ExecuteStatement(sql)).ToList();
    }

    public static async Task<int> ExecuteScalarIntAsync(IConnectionSettings connectionSettings, string sql)
    {
        var results = await ExecuteAsync(connectionSettings, sql);
        var result = Assert.Single(results);
        var reader = Assert.IsAssignableFrom<DbDataReader>(result.DataReader);
        try
        {
            Assert.True(await reader.ReadAsync());
            return Convert.ToInt32(reader.GetValue(0));
        }
        finally
        {
            await result.CloseDataReader();
        }
    }

    public static async Task<int> ExecuteNonQueryAsync(
        IConnectionSettings connectionSettings,
        string sql,
        IReadOnlyDictionary<string, object?>? parameters = null)
    {
        var provider = connectionSettings.GetDatabaseProvider();
        await using var connection = provider.GetConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandType = CommandType.Text;
        AddParameters(connection, command, parameters);
        return await command.ExecuteNonQueryAsync();
    }

    public static async Task<QuerySnapshot> ExecuteQuerySnapshotAsync(
        IConnectionSettings connectionSettings,
        string sql,
        IReadOnlyDictionary<string, object?>? parameters = null)
    {
        var provider = connectionSettings.GetDatabaseProvider();
        await using var connection = provider.GetConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandType = CommandType.Text;
        AddParameters(connection, command, parameters);
        await using var reader = await command.ExecuteReaderAsync();

        var columns = Enumerable.Range(0, reader.FieldCount)
            .Select(reader.GetName)
            .ToList();
        var schema = reader.GetColumnSchema();
        var rows = new List<object?[]>();

        while (await reader.ReadAsync())
        {
            var values = new object[reader.FieldCount];
            reader.GetValues(values);
            rows.Add(values);
        }

        return new QuerySnapshot(columns, rows, schema);
    }

    public static string QuoteSqlLiteral(string value)
    {
        return "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
    }

    private static void AddParameters(
        DbConnection connection,
        DbCommand command,
        IReadOnlyDictionary<string, object?>? parameters)
    {
        if (parameters is null)
            return;

        foreach (var parameter in parameters)
        {
            var dbParameter = command.CreateParameter();
            dbParameter.ParameterName = NormalizeParameterName(connection, parameter.Key);
            dbParameter.Value = parameter.Value ?? DBNull.Value;
            command.Parameters.Add(dbParameter);
        }
    }

    private static string NormalizeParameterName(DbConnection connection, string parameterName)
    {
        var trimmedName = parameterName.TrimStart('@', ':');
        return connection.GetType().Namespace?.Contains("Oracle", StringComparison.OrdinalIgnoreCase) == true
            ? trimmedName
            : "@" + trimmedName;
    }

    private static string GetSqLiteSeedSql()
    {
        return """
               create table customers
               (
                   customer_id integer primary key autoincrement,
                   name text not null,
                   email text,
                   created_at text default current_timestamp not null
               );

               create table orders
               (
                   order_id integer primary key autoincrement,
                   customer_id integer not null,
                   order_total numeric(10,2) not null,
                   status text default 'OPEN' not null,
                   created_at text default current_timestamp not null,
                   foreign key (customer_id) references customers(customer_id)
               );

               insert into customers (name, email) values ('Alice Johnson', 'alice@example.com');
               insert into customers (name, email) values ('Bob Smith', 'bob@example.com');

               insert into orders (customer_id, order_total, status) values (1, 149.90, 'OPEN');
               insert into orders (customer_id, order_total, status) values (2, 79.50, 'SHIPPED');

               create view open_orders as
               select
                   o.order_id,
                   c.name as customer_name,
                   o.order_total,
                   o.status,
                   o.created_at
               from orders o
               join customers c on c.customer_id = o.customer_id
               where o.status = 'OPEN';
               """;
    }

    internal sealed record QuerySnapshot(
        IReadOnlyList<string> Columns,
        IReadOnlyList<object?[]> Rows,
        IReadOnlyList<DbColumn> Schema);
}
