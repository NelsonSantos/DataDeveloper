// Cross-platform file service to manage folders and files in a writable app-specific directory

using System.Text.Json;
using System.Text.Json.Serialization;

namespace DataDeveloper.Core;

public class AppDataFileService
{
    public static string AppDataDirectory { get; } = InitializeAppDataDirectory();

    private static string InitializeAppDataDirectory()
    {
        var platform = AppDataLocation.CurrentPlatform;
        var appPath = AppDataLocation.GetAppDataPath(
            platform,
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetEnvironmentVariable("XDG_CONFIG_HOME"));
        var legacyPath = AppDataLocation.GetLegacyAppDataPath(platform, Environment.GetFolderPath(Environment.SpecialFolder.Personal));

        try
        {
            var outcome = AppDataLocation.MigrateLegacyData(legacyPath, appPath);
            if (outcome != AppDataLocation.MigrationOutcome.NotNeeded)
                LogMigration(appPath, $"{outcome} app data from {legacyPath} to {appPath}");
        }
        catch (Exception exception) when (legacyPath is not null && Directory.Exists(legacyPath))
        {
            // Keep using the old folder rather than starting without the user's connections; try again next launch.
            LogMigration(legacyPath, $"Could not move app data from {legacyPath} to {appPath}: {exception}");
            return legacyPath;
        }

        Directory.CreateDirectory(appPath);
        return appPath;
    }

    private static void LogMigration(string appPath, string message)
    {
        try
        {
            var logs = Directory.CreateDirectory(Path.Combine(appPath, "logs"));
            File.AppendAllText(Path.Combine(logs.FullName, "app-data-migration.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
        }
        catch
        {
            // Logging must not stop the app from starting.
        }
    }

    public void AppendLog(string fileName, string message, string? subfolder = null)
    {
        var dir = EnsureSubfolder(subfolder);
        var fullPath = Path.Combine(dir, fileName);
        File.AppendAllText(fullPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
    }

    public void WriteFile(string fileName, string content, string? subfolder = null)
    {
        var dir = EnsureSubfolder(subfolder);
        var fullPath = Path.Combine(dir, fileName);
        File.WriteAllText(fullPath, content);
    }

    public string? ReadFile(string fileName, string? subfolder = null)
    {
        var dir = EnsureSubfolder(subfolder);
        var fullPath = Path.Combine(dir, fileName);
        return File.Exists(fullPath) ? File.ReadAllText(fullPath) : null;
    }

    public void SaveJson<T>(string fileName, T data, string? subfolder = null, params JsonConverter[] converters)
    {
        var json = JsonSerializer.Serialize(data, MappingExtensions.GetJsonSerializerOptions(converters));
        WriteFile(fileName, json, subfolder);
    }

    public T? LoadJson<T>(string fileName, string? subfolder = null, params JsonConverter[] converters)
    {
        var content = ReadFile(fileName, subfolder);
        return content is not null ? JsonSerializer.Deserialize<T>(content, MappingExtensions.GetJsonSerializerOptions(converters)) : default;
    }

    public string GetDirectory(string? subfolder = null)
    {
        return EnsureSubfolder(subfolder);
    }

    public string GetFullPath(string fileName, string? subfolder = null)
    {
        return Path.Combine(EnsureSubfolder(subfolder), fileName);
    }

    private string EnsureSubfolder(string? subfolder)
    {
        if (string.IsNullOrWhiteSpace(subfolder))
            return AppDataDirectory;

        var fullPath = Path.Combine(AppDataDirectory, subfolder);
        Directory.CreateDirectory(fullPath);
        return fullPath;
    }
}
