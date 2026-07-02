using System.Data.Common;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Providers.SqlServer;
using DataDeveloper.Data.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DataDeveloper.Tests;

public class SchemaExplorerTests
{
    private sealed class FakeDatabaseProvider : IDatabaseProvider
    {
        public DbConnection GetConnection()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            return connection;
        }

        public TestConnectionResult TestConnection() => new(true, "ok");
        public IReadOnlyList<string> GetAvailableDatabaseNames() => [];
        public string GetTableStatement() => "select cast(null as text) as Name where 1 = 0";
        public string GetViewStatement() => "select cast(null as text) as Name where 1 = 0";
        public string GetColumnStatement() => "select cast(null as text) as Name where 1 = 0";
        public string GetProcedureStatement() => "select cast(null as text) as Name, cast(null as text) as SpecificName where 1 = 0";
        public string GetFunctionStatement() => "select cast(null as text) as Name, cast(null as text) as SpecificName, cast(null as text) as DataType where 1 = 0";
        public string GetRoutineParameterStatement() => "select cast(null as text) as Name where 1 = 0";
        public string GetColumnDefaultValueStatement() => "select cast(null as text) as ColumnName where 1 = 0";
        public string GetPrimaryKeyStatement() => "select cast(null as text) as ColumnName where 1 = 0";
        public string GetForeignKeyStatement() => "select cast(null as text) as ColumnName where 1 = 0";
        public string GetIndexStatement() => "select cast(null as text) as ColumnName where 1 = 0";
    }

    [Fact]
    public async Task InitializeSchemaNode_UsesConnectionNameWithDatabase_ForRootNode()
    {
        var connection = new SqlServerConnectionSettings
        {
            DatabaseType = DatabaseType.SqlServer,
            Name = "Oreons-Backoffice",
            Server = "localhost",
            Database = "NXGenMarketplace_Backoffice_Development"
        };
        var explorer = new SchemaExplorer(new FakeDatabaseProvider(), connection);

        await explorer.InitializeSchemaNode();

        var root = Assert.Single(explorer.RootConnections);
        Assert.Equal(
            "Oreons-Backoffice (NXGenMarketplace_Backoffice_Development)",
            root.Name);
    }
}
