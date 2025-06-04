using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Providers.SqlServer;

namespace DataDeveloper.Data.Services;

public class DatabaseProviderFactoryService
{
    public IDatabaseProvider? GetDatabaseProvider(IConnectionSettings connectionSettings)
    {
        return connectionSettings.DatabaseType switch
        {
            DatabaseType.SqlServer => new SqlServerDatabaseProvider((SqlServerConnectionSettings)connectionSettings),
            _ => throw new NotImplementedException($"Database type {connectionSettings.DatabaseType} is not implemented")
        };
    }

    public ISchemaExplorer? GetSchemaExplorer(IConnectionSettings connectionSettings)
    {
        var provider = GetDatabaseProvider(connectionSettings);
        return new SchemaExplorer(provider, connectionSettings);
    }

    public IStatementExecutor? GetStatementExecutor(IConnectionSettings connectionSettings)
    {
        return new StatementExecutor(connectionSettings);
    }
}