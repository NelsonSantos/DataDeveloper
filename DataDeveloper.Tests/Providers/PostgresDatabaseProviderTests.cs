using DataDeveloper.Data.Services.Metadata;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Providers.PostgresSql;
using Npgsql;
using Xunit;

namespace DataDeveloper.Tests.Providers;

public class PostgresDatabaseProviderTests
{
    [Fact]
    public void GetConnection_ReturnsNpgsqlConnection()
    {
        var provider = new PostgresDatabaseProvider(new PostgresConnectionSettings
        {
            Server = "localhost",
            Database = "app",
            User = "postgres",
            Password = "pwd",
            Port = 5433,
            Encrypt = false,
            TrustServerCertificate = false
        });

        var connection = provider.GetConnection();

        var npgsqlConnection = Assert.IsType<NpgsqlConnection>(connection);
        var builder = new NpgsqlConnectionStringBuilder(npgsqlConnection.ConnectionString);
        Assert.Equal("localhost", builder.Host);
        Assert.Equal("app", builder.Database);
        Assert.Equal("postgres", builder.Username);
        Assert.Equal(5433, builder.Port);
        Assert.Equal(SslMode.Disable, builder.SslMode);
    }

    [Fact]
    public void GetTableStatement_UsesInformationSchemaTables()
    {
        var catalog = ObjectCatalog.For(DatabaseType.PostgresSql);

        var sql = catalog.GetObjectListStatement(DbObjectKind.Table);

        Assert.Contains("information_schema.tables", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("table_schema = current_schema()", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("table_type = 'BASE TABLE'", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetColumnStatement_UsesInformationSchemaColumns()
    {
        var catalog = ObjectCatalog.For(DatabaseType.PostgresSql);

        var sql = catalog.GetColumnsStatement();

        Assert.Contains("information_schema.columns", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("information_schema.key_column_usage", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("information_schema.table_constraints", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("table_name = @TableName", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("is_identity", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("HasDefaultValue", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetViewStatement_UsesInformationSchemaViews()
    {
        var catalog = ObjectCatalog.For(DatabaseType.PostgresSql);

        var sql = catalog.GetObjectListStatement(DbObjectKind.View);

        Assert.Contains("information_schema.views", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("table_schema = current_schema()", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetProcedureStatement_UsesInformationSchemaRoutines()
    {
        var catalog = ObjectCatalog.For(DatabaseType.PostgresSql);

        var sql = catalog.GetObjectListStatement(DbObjectKind.Procedure);

        Assert.Contains("information_schema.routines", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("routine_type = 'PROCEDURE'", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("specific_schema = current_schema()", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetFunctionStatement_UsesInformationSchemaRoutines()
    {
        var catalog = ObjectCatalog.For(DatabaseType.PostgresSql);

        var sql = catalog.GetObjectListStatement(DbObjectKind.Function);

        Assert.Contains("information_schema.routines", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("routine_type = 'FUNCTION'", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("specific_schema = current_schema()", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetRoutineParameterStatement_UsesInformationSchemaParameters()
    {
        var catalog = ObjectCatalog.For(DatabaseType.PostgresSql);

        var sql = catalog.GetRoutineParametersStatement();

        Assert.Contains("information_schema.parameters", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("specific_name = @SpecificName", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("specific_schema = current_schema()", sql, StringComparison.OrdinalIgnoreCase);
    }
}
