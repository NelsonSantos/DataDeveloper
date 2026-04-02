using DataDeveloper.Data.Providers.SqLite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DataDeveloper.Tests.Providers;

public class SqLiteDatabaseProviderTests
{
    [Fact]
    public void GetConnection_ReturnsSqliteConnection()
    {
        var provider = new SqLiteDatabaseProvider(new SqLiteConnectionSettings
        {
            Database = "/tmp/app.db"
        });

        var connection = provider.GetConnection();

        var sqliteConnection = Assert.IsType<SqliteConnection>(connection);
        var builder = new SqliteConnectionStringBuilder(sqliteConnection.ConnectionString);
        Assert.Equal("/tmp/app.db", builder.DataSource);
    }

    [Fact]
    public void GetTableStatement_UsesSqliteMaster()
    {
        var provider = new SqLiteDatabaseProvider(new SqLiteConnectionSettings());

        var sql = provider.GetTableStatement();

        Assert.Contains("from sqlite_master", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("type = 'table'", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetColumnStatement_UsesPragmaTableInfo()
    {
        var provider = new SqLiteDatabaseProvider(new SqLiteConnectionSettings());

        var sql = provider.GetColumnStatement();

        Assert.Contains("pragma_table_info(__table_name__)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("p.\"notnull\"", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetViewStatement_UsesSqliteMaster()
    {
        var provider = new SqLiteDatabaseProvider(new SqLiteConnectionSettings());

        var sql = provider.GetViewStatement();

        Assert.Contains("from sqlite_master", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("type = 'view'", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetProcedureStatement_ReturnsEmptyProjection()
    {
        var provider = new SqLiteDatabaseProvider(new SqLiteConnectionSettings());

        var sql = provider.GetProcedureStatement();

        Assert.Contains("where 1 = 0", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetFunctionStatement_ReturnsEmptyProjection()
    {
        var provider = new SqLiteDatabaseProvider(new SqLiteConnectionSettings());

        var sql = provider.GetFunctionStatement();

        Assert.Contains("where 1 = 0", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetRoutineParameterStatement_ReturnsEmptyProjection()
    {
        var provider = new SqLiteDatabaseProvider(new SqLiteConnectionSettings());

        var sql = provider.GetRoutineParameterStatement();

        Assert.Contains("where 1 = 0", sql, StringComparison.OrdinalIgnoreCase);
    }
}
