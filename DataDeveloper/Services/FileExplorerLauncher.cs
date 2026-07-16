using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace DataDeveloper.Services;

public static class FileExplorerLauncher
{
    public static Task RevealAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return Task.CompletedTask;

        if (OperatingSystem.IsWindows())
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{filePath}\"",
                UseShellExecute = true
            });
            return Task.CompletedTask;
        }

        if (OperatingSystem.IsMacOS())
        {
            Process.Start("/usr/bin/open", ["-R", filePath]);
            return Task.CompletedTask;
        }

        if (OperatingSystem.IsLinux())
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Process.Start("/usr/bin/xdg-open", [directory]);
            return Task.CompletedTask;
        }

        return Task.CompletedTask;
    }
}
