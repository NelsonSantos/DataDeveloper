using DataDeveloper.Data.Providers.MySql;
using MySqlConnector;
using Xunit;

namespace DataDeveloper.Tests.Providers;

public class MySqlDatabaseProviderTests
{
    [Fact]
    public void GetConnection_ReturnsMySqlConnection()
    {
        var provider = new MySqlDatabaseProvider(new MySqlConnectionSettings
        {
            Server = "localhost",
            Database = "app",
            User = "root",
            Password = "pwd",
            Port = 3307,
            Encrypt = false,
            TrustServerCertificate = true
        });

        var connection = provider.GetConnection();

        var mySqlConnection = Assert.IsType<MySqlConnection>(connection);
        var builder = new MySqlConnectionStringBuilder(mySqlConnection.ConnectionString);
        Assert.Equal("localhost", builder.Server);
        Assert.Equal("app", builder.Database);
        Assert.Equal((uint)3307, builder.Port);
        Assert.Equal(MySqlSslMode.None, builder.SslMode);
    }

    [Fact]
    public void GetTableStatement_UsesInformationSchemaTables()
    {
        var provider = new MySqlDatabaseProvider(new MySqlConnectionSettings());

        var sql = provider.GetTableStatement();

        Assert.Contains("information_schema.tables", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("table_schema = database()", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetColumnStatement_UsesInformationSchemaColumns()
    {
        var provider = new MySqlDatabaseProvider(new MySqlConnectionSettings());

        var sql = provider.GetColumnStatement();

        Assert.Contains("information_schema.columns", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("information_schema.key_column_usage", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("table_name = @TableName", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetViewStatement_UsesInformationSchemaViews()
    {
        var provider = new MySqlDatabaseProvider(new MySqlConnectionSettings());

        var sql = provider.GetViewStatement();

        Assert.Contains("information_schema.views", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("table_schema = database()", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetProcedureStatement_UsesInformationSchemaRoutines()
    {
        var provider = new MySqlDatabaseProvider(new MySqlConnectionSettings());

        var sql = provider.GetProcedureStatement();

        Assert.Contains("information_schema.routines", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("routine_type = 'PROCEDURE'", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetFunctionStatement_UsesInformationSchemaRoutines()
    {
        var provider = new MySqlDatabaseProvider(new MySqlConnectionSettings());

        var sql = provider.GetFunctionStatement();

        Assert.Contains("information_schema.routines", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("routine_type = 'FUNCTION'", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetRoutineParameterStatement_UsesInformationSchemaParameters()
    {
        var provider = new MySqlDatabaseProvider(new MySqlConnectionSettings());

        var sql = provider.GetRoutineParameterStatement();

        Assert.Contains("information_schema.parameters", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("specific_name = @SpecificName", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("is_nullable", sql, StringComparison.OrdinalIgnoreCase);
    }
}
