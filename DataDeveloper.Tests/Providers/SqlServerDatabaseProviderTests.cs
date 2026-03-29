using DataDeveloper.Data.Providers.SqlServer;
using Microsoft.Data.SqlClient;
using Xunit;

namespace DataDeveloper.Tests.Providers;

public class SqlServerDatabaseProviderTests
{
    [Fact]
    public void GetConnection_ReturnsSqlConnection()
    {
        var provider = new SqlServerDatabaseProvider(new SqlServerConnectionSettings
        {
            Server = "localhost",
            Database = "master",
            User = "sa",
            Password = "pwd",
            Encrypt = true,
            TrustServerCertificate = false
        });

        var connection = provider.GetConnection();

        var sqlConnection = Assert.IsType<SqlConnection>(connection);
        var builder = new SqlConnectionStringBuilder(sqlConnection.ConnectionString);
        Assert.Equal("localhost", builder.DataSource);
        Assert.Equal("master", builder.InitialCatalog);
        Assert.True(builder.Encrypt);
        Assert.False(builder.TrustServerCertificate);
    }

    [Fact]
    public void GetTableStatement_UsesInformationSchemaTables()
    {
        var provider = new SqlServerDatabaseProvider(new SqlServerConnectionSettings());

        var sql = provider.GetTableStatement();

        Assert.Contains("information_schema.tables", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("table_type = 'BASE TABLE'", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetColumnStatement_UsesSysCatalogs()
    {
        var provider = new SqlServerDatabaseProvider(new SqlServerConnectionSettings());

        var sql = provider.GetColumnStatement();

        Assert.Contains("sys.columns", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sys.types", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OBJECT_ID(@TableName)", sql, StringComparison.OrdinalIgnoreCase);
    }
}
