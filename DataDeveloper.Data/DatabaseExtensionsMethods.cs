using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DataDeveloper.Data;

public static class DatabaseExtensionsMethods
{
    private static IServiceProvider? _serviceProvider;
    public static void SetServiceProvider(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    private static DatabaseProviderFactoryService GetFactory()
    {
        return _serviceProvider?.GetRequiredService<DatabaseProviderFactoryService>()
               ?? throw new InvalidOperationException("Database service provider was not initialized.");
    }

    public static IDatabaseProvider GetDatabaseProvider(this IConnectionSettings value)
    {
        return GetFactory().GetDatabaseProvider(value);
    }
    public static ISchemaExplorer GetSchemaExplorer(this IConnectionSettings value)
    {
        return GetFactory().GetSchemaExplorer(value);
    }

    public static IStatementExecutor GetStatementExecutor(this IConnectionSettings value)
    {
        return GetFactory().GetStatementExecutor(value);
    }

    public static IProviderSqlAnalyzer GetSqlAnalyzer(this IConnectionSettings value)
    {
        return GetFactory().GetSqlAnalyzer(value);
    }
}
