// Cross-platform file service to manage folders and files in a writable app-specific directory
using System;
using System.IO;
using System.Text.Json;

namespace DataDeveloper.Services;

public class AppDataFileService
{
    private const string AppFolderName = "DataDeveloper";
    private readonly string _appDataDirectory;

    public AppDataFileService()
    {
        _appDataDirectory = InitializeAppDataDirectory();
    }

    private string InitializeAppDataDirectory()
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

    public string AppDataDirectory => _appDataDirectory;

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

    public void SaveJson<T>(string fileName, T data, string? subfolder = null)
    {
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        WriteFile(fileName, json, subfolder);
    }

    public T? LoadJson<T>(string fileName, string? subfolder = null)
    {
        var content = ReadFile(fileName, subfolder);
        return content is not null ? JsonSerializer.Deserialize<T>(content) : default;
    }

    private string EnsureSubfolder(string? subfolder)
    {
        if (string.IsNullOrWhiteSpace(subfolder))
            return _appDataDirectory;

        var fullPath = Path.Combine(_appDataDirectory, subfolder);
        Directory.CreateDirectory(fullPath);
        return fullPath;
    }
}
