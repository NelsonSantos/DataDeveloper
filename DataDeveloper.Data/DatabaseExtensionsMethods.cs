using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DataDeveloper.Data;

public static class DatabaseExtensionsMethods
{
    private static IServiceProvider _serviceProvider;
    public static void SetServiceProvider(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    public static IDatabaseProvider GetDatabaseProvider(this IConnectionSettings value)
    {
        var factory = _serviceProvider.GetService<DatabaseProviderFactoryService>();
        var provider = factory.GetDatabaseProvider(value);
        return provider;
    }
    public static ISchemaExplorer GetSchemaExplorer(this IConnectionSettings value)
    {
        var factory = _serviceProvider.GetService<DatabaseProviderFactoryService>();
        var explorer = factory.GetSchemaExplorer(value);
        return explorer;
    }
}