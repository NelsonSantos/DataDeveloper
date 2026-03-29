using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Providers.MySql;
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
}
