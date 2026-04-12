using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Providers.Oracle;
using DataDeveloper.Data.Providers.MySql;
using DataDeveloper.Data.Providers.PostgresSql;
using DataDeveloper.Data.Providers.SqLite;
using DataDeveloper.Data.Providers.SqlServer;
using DataDeveloper.Data.Services;
using Xunit;

namespace DataDeveloper.Tests.Providers;

public class DatabaseProviderFactoryServiceTests
{
    [Fact]
    public void GetDatabaseProvider_ReturnsSqlServerProvider_ForSqlServerConnection()
    {
        var factory = new DatabaseProviderFactoryService();
        var connection = new SqlServerConnectionSettings
        {
            DatabaseType = DatabaseType.SqlServer,
            Server = "localhost",
            Database = "app"
        };

        var provider = factory.GetDatabaseProvider(connection);

        Assert.IsType<SqlServerDatabaseProvider>(provider);
    }

    [Fact]
    public void GetDatabaseProvider_ReturnsMySqlProvider_ForMySqlConnection()
    {
        var factory = new DatabaseProviderFactoryService();
        var connection = new MySqlConnectionSettings
        {
            DatabaseType = DatabaseType.MySql,
            Server = "localhost",
            Database = "app"
        };

        var provider = factory.GetDatabaseProvider(connection);

        Assert.IsType<MySqlDatabaseProvider>(provider);
    }

    [Fact]
    public void GetDatabaseProvider_ReturnsOracleProvider_ForOracleConnection()
    {
        var factory = new DatabaseProviderFactoryService();
        var connection = new OracleConnectionSettings
        {
            DatabaseType = DatabaseType.Oracle,
            Server = "localhost",
            Database = "xe"
        };

        var provider = factory.GetDatabaseProvider(connection);

        Assert.IsType<OracleDatabaseProvider>(provider);
    }

    [Fact]
    public void GetDatabaseProvider_ReturnsPostgresProvider_ForPostgresConnection()
    {
        var factory = new DatabaseProviderFactoryService();
        var connection = new PostgresConnectionSettings
        {
            DatabaseType = DatabaseType.PostgresSql,
            Server = "localhost",
            Database = "app"
        };

        var provider = factory.GetDatabaseProvider(connection);

        Assert.IsType<PostgresDatabaseProvider>(provider);
    }

    [Fact]
    public void GetDatabaseProvider_ReturnsSqLiteProvider_ForSqLiteConnection()
    {
        var factory = new DatabaseProviderFactoryService();
        var connection = new SqLiteConnectionSettings
        {
            DatabaseType = DatabaseType.SqLite,
            Database = "/tmp/app.db"
        };

        var provider = factory.GetDatabaseProvider(connection);

        Assert.IsType<SqLiteDatabaseProvider>(provider);
    }

    [Fact]
    public void GetSchemaExplorer_ReturnsExplorer_ForSqlServerConnection()
    {
        var factory = new DatabaseProviderFactoryService();
        IConnectionSettings connection = new SqlServerConnectionSettings
        {
            DatabaseType = DatabaseType.SqlServer,
            Server = "localhost",
            Database = "app"
        };

        var explorer = factory.GetSchemaExplorer(connection);

        Assert.NotNull(explorer);
    }

    [Fact]
    public void GetSchemaExplorer_ReturnsExplorer_ForOracleConnection()
    {
        var factory = new DatabaseProviderFactoryService();
        IConnectionSettings connection = new OracleConnectionSettings
        {
            DatabaseType = DatabaseType.Oracle,
            Server = "localhost",
            Database = "xe"
        };

        var explorer = factory.GetSchemaExplorer(connection);

        Assert.NotNull(explorer);
    }

    [Fact]
    public void GetSchemaExplorer_ReturnsExplorer_ForMySqlConnection()
    {
        var factory = new DatabaseProviderFactoryService();
        IConnectionSettings connection = new MySqlConnectionSettings
        {
            DatabaseType = DatabaseType.MySql,
            Server = "localhost",
            Database = "app"
        };

        var explorer = factory.GetSchemaExplorer(connection);

        Assert.NotNull(explorer);
    }

    [Fact]
    public void GetSchemaExplorer_ReturnsExplorer_ForPostgresConnection()
    {
        var factory = new DatabaseProviderFactoryService();
        IConnectionSettings connection = new PostgresConnectionSettings
        {
            DatabaseType = DatabaseType.PostgresSql,
            Server = "localhost",
            Database = "app"
        };

        var explorer = factory.GetSchemaExplorer(connection);

        Assert.NotNull(explorer);
    }

    [Fact]
    public void GetSchemaExplorer_ReturnsExplorer_ForSqLiteConnection()
    {
        var factory = new DatabaseProviderFactoryService();
        IConnectionSettings connection = new SqLiteConnectionSettings
        {
            DatabaseType = DatabaseType.SqLite,
            Database = "/tmp/app.db"
        };

        var explorer = factory.GetSchemaExplorer(connection);

        Assert.NotNull(explorer);
    }

    [Theory]
    [InlineData(DatabaseType.SqlServer)]
    [InlineData(DatabaseType.MySql)]
    [InlineData(DatabaseType.PostgresSql)]
    [InlineData(DatabaseType.Oracle)]
    [InlineData(DatabaseType.SqLite)]
    public void GetSqlAnalyzer_ReturnsProviderAnalyzer_ForSupportedProviders(DatabaseType databaseType)
    {
        var factory = new DatabaseProviderFactoryService();
        var connection = CreateConnection(databaseType);

        var analyzer = factory.GetSqlAnalyzer(connection);

        Assert.NotNull(analyzer);
        Assert.Equal(ExpectedAnalyzerType(databaseType), analyzer.GetType());
        Assert.True(analyzer.IsDmlStatement("update items set name = 'changed'"));
    }

    private static Type ExpectedAnalyzerType(DatabaseType databaseType)
    {
        return databaseType switch
        {
            DatabaseType.SqlServer => typeof(SqlServerSqlAnalyzer),
            DatabaseType.MySql => typeof(MySqlSqlAnalyzer),
            DatabaseType.PostgresSql => typeof(PostgresSqlAnalyzer),
            DatabaseType.Oracle => typeof(OracleSqlAnalyzer),
            DatabaseType.SqLite => typeof(SqLiteSqlAnalyzer),
            _ => throw new NotSupportedException($"Database type {databaseType} is not supported by this test.")
        };
    }

    private static IConnectionSettings CreateConnection(DatabaseType databaseType)
    {
        return databaseType switch
        {
            DatabaseType.SqlServer => new SqlServerConnectionSettings
            {
                DatabaseType = DatabaseType.SqlServer,
                Server = "localhost",
                Database = "app"
            },
            DatabaseType.MySql => new MySqlConnectionSettings
            {
                DatabaseType = DatabaseType.MySql,
                Server = "localhost",
                Database = "app"
            },
            DatabaseType.PostgresSql => new PostgresConnectionSettings
            {
                DatabaseType = DatabaseType.PostgresSql,
                Server = "localhost",
                Database = "app"
            },
            DatabaseType.Oracle => new OracleConnectionSettings
            {
                DatabaseType = DatabaseType.Oracle,
                Server = "localhost",
                Database = "xe"
            },
            DatabaseType.SqLite => new SqLiteConnectionSettings
            {
                DatabaseType = DatabaseType.SqLite,
                Database = "/tmp/app.db"
            },
            _ => throw new NotSupportedException($"Database type {databaseType} is not supported by this test.")
        };
    }
}
