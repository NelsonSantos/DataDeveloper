using DataDeveloper.Data;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Services;
using DataDeveloper.Core;
using Microsoft.Extensions.DependencyInjection;
using DataDeveloper.Interfaces;
using DataDeveloper.Services;

namespace DataDeveloper.Tests.Integration;

internal static class DatabaseIntegrationTestSupport
{
    private const string IntegrationFlag = "RUN_DB_INTEGRATION_TESTS";

    public static bool ShouldRunIntegrationTests()
    {
        var value = Environment.GetEnvironmentVariable(IntegrationFlag);
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    public static ConnectionSettings? TryLoadConnection(string connectionName)
    {
        var appDataFileService = new AppDataFileService();
        ISecretStore secretStore = new PlatformSecretStore(appDataFileService);
        var repository = new SqliteConnectionSettingsRepository(appDataFileService, secretStore);
        var connection = repository.LoadAll()
            .FirstOrDefault(c => string.Equals(c.Name, connectionName, StringComparison.OrdinalIgnoreCase));

        if (connection is null)
            return null;

        repository.LoadPassword(connection);
        return connection;
    }

    public static void EnsureDatabaseServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<DatabaseProviderFactoryService>();
        DatabaseExtensionsMethods.SetServiceProvider(services.BuildServiceProvider());
    }

    public static async Task<T> WithTimeout<T>(Task<T> task, TimeSpan timeout, string operationName)
    {
        var completed = await Task.WhenAny(task, Task.Delay(timeout));
        if (completed != task)
            throw new TimeoutException($"{operationName} timed out after {timeout}.");

        return await task;
    }

    public static async Task WithTimeout(Task task, TimeSpan timeout, string operationName)
    {
        var completed = await Task.WhenAny(task, Task.Delay(timeout));
        if (completed != task)
            throw new TimeoutException($"{operationName} timed out after {timeout}.");

        await task;
    }
}
