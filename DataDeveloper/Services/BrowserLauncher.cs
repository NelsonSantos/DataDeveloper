using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace DataDeveloper.Services;

public static class BrowserLauncher
{
    public static Task OpenAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return Task.CompletedTask;

        if (OperatingSystem.IsWindows())
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
            return Task.CompletedTask;
        }

        if (OperatingSystem.IsMacOS())
        {
            Process.Start("/usr/bin/open", [url]);
            return Task.CompletedTask;
        }

        if (OperatingSystem.IsLinux())
        {
            Process.Start("/usr/bin/xdg-open", [url]);
            return Task.CompletedTask;
        }

        return Task.CompletedTask;
    }
}
