using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Providers.Oracle;
using DataDeveloper.Data.Providers.MySql;
using DataDeveloper.Data.Providers.PostgresSql;
using DataDeveloper.Data.Providers.SqLite;
using DataDeveloper.Data.Providers.SqlServer;

namespace DataDeveloper.Data.Services;

public class DatabaseProviderFactoryService
{
    public virtual IDatabaseProvider GetDatabaseProvider(IConnectionSettings connectionSettings)
    {
        return connectionSettings.DatabaseType switch
        {
            DatabaseType.SqlServer => new SqlServerDatabaseProvider((SqlServerConnectionSettings)connectionSettings),
            DatabaseType.Oracle => new OracleDatabaseProvider((OracleConnectionSettings)connectionSettings),
            DatabaseType.PostgresSql => new PostgresDatabaseProvider((PostgresConnectionSettings)connectionSettings),
            DatabaseType.MySql => new MySqlDatabaseProvider((MySqlConnectionSettings)connectionSettings),
            DatabaseType.SqLite => new SqLiteDatabaseProvider((SqLiteConnectionSettings)connectionSettings),
            _ => throw new NotImplementedException($"Database type {connectionSettings.DatabaseType} is not implemented")
        };
    }

    public ISchemaExplorer GetSchemaExplorer(IConnectionSettings connectionSettings)
    {
        var provider = GetDatabaseProvider(connectionSettings);
        return new SchemaExplorer(provider, connectionSettings);
    }

    public IStatementExecutor GetStatementExecutor(IConnectionSettings connectionSettings)
    {
        return new StatementExecutor(connectionSettings);
    }

    public IProviderSqlAnalyzer GetSqlAnalyzer(IConnectionSettings connectionSettings)
    {
        return ProviderSqlAnalyzer.Create(connectionSettings.DatabaseType);
    }
}
