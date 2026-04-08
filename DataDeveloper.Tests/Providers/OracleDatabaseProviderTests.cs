using DataDeveloper.Data.Providers.Oracle;
using Oracle.ManagedDataAccess.Client;
using Xunit;

namespace DataDeveloper.Tests.Providers;

public class OracleDatabaseProviderTests
{
    [Fact]
    public void GetConnection_ReturnsOracleConnection()
    {
        var provider = new OracleDatabaseProvider(new OracleConnectionSettings
        {
            Server = "localhost",
            Database = "xe",
            User = "system",
            Password = "pwd",
            Port = 1522
        });

        var connection = provider.GetConnection();

        var oracleConnection = Assert.IsType<OracleConnection>(connection);
        var builder = new OracleConnectionStringBuilder(oracleConnection.ConnectionString);
        Assert.Equal("localhost:1522/xe", builder.DataSource);
        Assert.Equal("system", builder.UserID);
    }

    [Fact]
    public void GetTableStatement_UsesUserTables()
    {
        var provider = new OracleDatabaseProvider(new OracleConnectionSettings());

        var sql = provider.GetTableStatement();

        Assert.Contains("from user_tables", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetColumnStatement_UsesUserTabColumns()
    {
        var provider = new OracleDatabaseProvider(new OracleConnectionSettings());

        var sql = provider.GetColumnStatement();

        Assert.Contains("from user_tab_columns", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("where c.table_name = upper(:TableName)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("from user_constraints", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("identity_column", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("HasDefaultValue", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("virtual_column", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetViewStatement_UsesUserViews()
    {
        var provider = new OracleDatabaseProvider(new OracleConnectionSettings());

        var sql = provider.GetViewStatement();

        Assert.Contains("from user_views", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetProcedureStatement_UsesUserObjects()
    {
        var provider = new OracleDatabaseProvider(new OracleConnectionSettings());

        var sql = provider.GetProcedureStatement();

        Assert.Contains("from user_objects", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("object_type = 'PROCEDURE'", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetFunctionStatement_UsesUserObjects()
    {
        var provider = new OracleDatabaseProvider(new OracleConnectionSettings());

        var sql = provider.GetFunctionStatement();

        Assert.Contains("from user_objects", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("object_type = 'FUNCTION'", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetRoutineParameterStatement_UsesUserArguments()
    {
        var provider = new OracleDatabaseProvider(new OracleConnectionSettings());

        var sql = provider.GetRoutineParameterStatement();

        Assert.Contains("from user_arguments", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("object_name = upper(:SpecificName)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("':' || lower(argument_name) as \"Name\"", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("in_out as \"Mode\"", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("position as \"Position\"", sql, StringComparison.OrdinalIgnoreCase);
    }
}
