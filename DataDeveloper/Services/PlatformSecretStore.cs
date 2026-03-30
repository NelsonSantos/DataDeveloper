using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Versioning;
using DataDeveloper.Core;
using DataDeveloper.Interfaces;

namespace DataDeveloper.Services;

public class PlatformSecretStore : ISecretStore
{
    private const string MacOsServiceName = "DataDeveloper";
    private readonly string _dpapiDirectoryPath;
    public bool IsAvailable { get; }
    public string? UnavailableReason { get; }

    public PlatformSecretStore(AppDataFileService fileService)
    {
        _dpapiDirectoryPath = fileService.GetDirectory("secrets");
        (IsAvailable, UnavailableReason) = DetectAvailability();
    }

    public async Task SaveAsync(string key, string secret)
    {
        EnsureAvailable();

        if (OperatingSystem.IsMacOS())
        {
            await SaveToMacOsKeychainAsync(key, secret);
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            await SaveToWindowsProtectedStoreAsync(key, secret);
            return;
        }

        if (OperatingSystem.IsLinux())
        {
            if (await TrySaveToLinuxSecretServiceAsync(key, secret))
                return;
        }

        throw new NotSupportedException("Secure secret storage is not available on this platform.");
    }

    public async Task<string?> GetAsync(string key)
    {
        EnsureAvailable();

        if (OperatingSystem.IsMacOS())
            return await GetFromMacOsKeychainAsync(key);

        if (OperatingSystem.IsWindows())
            return await GetFromWindowsProtectedStoreAsync(key);

        if (OperatingSystem.IsLinux())
            return await TryGetFromLinuxSecretServiceAsync(key);

        throw new NotSupportedException("Secure secret storage is not available on this platform.");
    }

    public async Task DeleteAsync(string key)
    {
        EnsureAvailable();

        if (OperatingSystem.IsMacOS())
        {
            await DeleteFromMacOsKeychainAsync(key);
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            DeleteFromWindowsProtectedStore(key);
            return;
        }

        if (OperatingSystem.IsLinux())
        {
            await TryDeleteFromLinuxSecretServiceAsync(key);
            return;
        }

        throw new NotSupportedException("Secure secret storage is not available on this platform.");
    }

    private (bool IsAvailable, string? Reason) DetectAvailability()
    {
        if (OperatingSystem.IsMacOS())
            return File.Exists("/usr/bin/security")
                ? (true, null)
                : (false, "macOS Keychain command 'security' is not available.");

        if (OperatingSystem.IsWindows())
            return (true, null);

        if (OperatingSystem.IsLinux())
            return IsExecutableAvailable("secret-tool")
                ? (true, null)
                : (false, "Linux Secret Service is not available. Install 'secret-tool' (libsecret) or configure a supported keyring.");

        return (false, "Secure credential storage is not available on this platform.");
    }

    private void EnsureAvailable()
    {
        if (!IsAvailable)
            throw new InvalidOperationException(UnavailableReason ?? "Secure credential storage is not available.");
    }

    private static bool IsExecutableAvailable(string executableName)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
            return false;

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var fullPath = Path.Combine(directory, executableName);
            if (File.Exists(fullPath))
                return true;
        }

        return false;
    }

    private static async Task SaveToMacOsKeychainAsync(string key, string secret)
    {
        await DeleteFromMacOsKeychainAsync(key);
        await RunProcessAsync(
            "/usr/bin/security",
            ["add-generic-password", "-a", key, "-s", MacOsServiceName, "-w", secret]);
    }

    private static async Task<string?> GetFromMacOsKeychainAsync(string key)
    {
        var result = await RunProcessAsync(
            "/usr/bin/security",
            ["find-generic-password", "-a", key, "-s", MacOsServiceName, "-w"],
            throwOnError: false);

        return result.ExitCode == 0 ? result.StdOut.TrimEnd() : null;
    }

    private static async Task DeleteFromMacOsKeychainAsync(string key)
    {
        await RunProcessAsync(
            "/usr/bin/security",
            ["delete-generic-password", "-a", key, "-s", MacOsServiceName],
            throwOnError: false);
    }

    [SupportedOSPlatform("windows")]
    private async Task SaveToWindowsProtectedStoreAsync(string key, string secret)
    {
        var path = GetWindowsSecretPath(key);
        var bytes = Encoding.UTF8.GetBytes(secret);
        var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        await File.WriteAllBytesAsync(path, protectedBytes);
    }

    [SupportedOSPlatform("windows")]
    private async Task<string?> GetFromWindowsProtectedStoreAsync(string key)
    {
        var path = GetWindowsSecretPath(key);
        if (!File.Exists(path))
            return null;

        var protectedBytes = await File.ReadAllBytesAsync(path);
        var bytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(bytes);
    }

    [SupportedOSPlatform("windows")]
    private void DeleteFromWindowsProtectedStore(string key)
    {
        var path = GetWindowsSecretPath(key);
        if (File.Exists(path))
            File.Delete(path);
    }

    private async Task<bool> TrySaveToLinuxSecretServiceAsync(string key, string secret)
    {
        var result = await RunProcessAsync(
            "/usr/bin/env",
            ["secret-tool", "store", $"--label=DataDeveloper {key}", "service", MacOsServiceName, "account", key],
            stdin: secret,
            throwOnError: false);

        return result.ExitCode == 0;
    }

    private static async Task<string?> TryGetFromLinuxSecretServiceAsync(string key)
    {
        var result = await RunProcessAsync(
            "/usr/bin/env",
            ["secret-tool", "lookup", "service", MacOsServiceName, "account", key],
            throwOnError: false);

        return result.ExitCode == 0 ? result.StdOut.TrimEnd() : null;
    }

    private static async Task TryDeleteFromLinuxSecretServiceAsync(string key)
    {
        await RunProcessAsync(
            "/usr/bin/env",
            ["secret-tool", "clear", "service", MacOsServiceName, "account", key],
            throwOnError: false);
    }

    private string GetWindowsSecretPath(string key)
    {
        var safeFileName = $"{key}.bin";
        return Path.Combine(_dpapiDirectoryPath, safeFileName);
    }

    private static async Task<ProcessResult> RunProcessAsync(string fileName, IEnumerable<string> arguments, string? stdin = null, bool throwOnError = true)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = stdin is not null,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        if (stdin is not null)
        {
            await process.StandardInput.WriteAsync(stdin);
            process.StandardInput.Close();
        }

        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        var result = new ProcessResult(await standardOutputTask, await standardErrorTask, process.ExitCode);
        if (throwOnError && result.ExitCode != 0)
            throw new InvalidOperationException($"Secret store command failed: {result.StdErr}");

        return result;
    }

    private readonly record struct ProcessResult(string StdOut, string StdErr, int ExitCode);
}
