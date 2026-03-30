// Cross-platform file service to manage folders and files in a writable app-specific directory

using System.Text.Json;
using System.Text.Json.Serialization;

namespace DataDeveloper.Core;

public class AppDataFileService
{
    private const string AppFolderName = "DataDeveloper";
    public static string AppDataDirectory { get; }= InitializeAppDataDirectory();

    private static string InitializeAppDataDirectory()
    {
        string basePath;

        if (OperatingSystem.IsWindows())
        {
            basePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData); // %AppData%
        }
        else if (OperatingSystem.IsMacOS())
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            basePath = Path.Combine(home, "Library", "Application Support");
        }
        else // Linux and fallback
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            basePath = Path.Combine(home, ".config");
        }

        var appPath = Path.Combine(basePath, AppFolderName);
        Directory.CreateDirectory(appPath); // ensure exists
        return appPath;
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
