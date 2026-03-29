using System.Text.Json;
using DataDeveloper.Data;
using DataDeveloper.Data.JsonConverters;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DataDeveloper.Tests.Integration;

internal static class DatabaseIntegrationTestSupport
{
    private const string DefaultConnectionsPath = "/Users/nelsosantos/Documents/Library/Application Support/DataDeveloper/connections/connections.json";
    private const string IntegrationFlag = "RUN_DB_INTEGRATION_TESTS";

    public static bool ShouldRunIntegrationTests()
    {
        var value = Environment.GetEnvironmentVariable(IntegrationFlag);
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    public static ConnectionSettings? TryLoadConnection(string connectionName)
    {
        var path = Environment.GetEnvironmentVariable("DATADEVELOPER_CONNECTIONS_FILE");
        if (string.IsNullOrWhiteSpace(path))
            path = DefaultConnectionsPath;

        if (!File.Exists(path))
            return null;

        var json = File.ReadAllText(path);
        var options = MappingExtensions.GetJsonSerializerOptions(new ConnectionSettingsConverter());
        var connections = JsonSerializer.Deserialize<List<ConnectionSettings>>(json, options);
        return connections?.FirstOrDefault(c => string.Equals(c.Name, connectionName, StringComparison.OrdinalIgnoreCase));
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
