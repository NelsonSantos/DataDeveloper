namespace DataDeveloper.Data.Ai;

/// <summary>
/// Finds a CLI executable. Apps launched from the Finder/Start menu don't inherit the shell PATH, so the
/// usual per-user install folders are searched in addition to PATH.
/// </summary>
public static class CliExecutableLocator
{
    public static string? Locate(string commandName, string? explicitPath = null)
    {
        return Locate(commandName, explicitPath, GetSearchDirectories(), OperatingSystem.IsWindows(), File.Exists);
    }

    public static string? Locate(
        string commandName,
        string? explicitPath,
        IEnumerable<string> searchDirectories,
        bool isWindows,
        Func<string, bool> fileExists)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            var trimmed = explicitPath.Trim();
            return fileExists(trimmed) ? trimmed : null;
        }

        var candidates = isWindows
            ? new[] { commandName + ".exe", commandName + ".cmd", commandName + ".bat" }
            : new[] { commandName };

        foreach (var directory in searchDirectories)
        {
            foreach (var candidate in candidates)
            {
                var path = Path.Combine(directory, candidate);
                if (fileExists(path))
                    return path;
            }
        }

        return null;
    }

    public static IReadOnlyList<string> GetSearchDirectories()
    {
        var directories = new List<string>();
        var pathVariable = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathVariable))
            directories.AddRange(pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries));

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home))
        {
            directories.Add(Path.Combine(home, ".local", "bin"));
            directories.Add(Path.Combine(home, ".claude", "local"));
            directories.Add(Path.Combine(home, ".npm-global", "bin"));
            directories.Add(Path.Combine(home, ".bun", "bin"));
            directories.Add(Path.Combine(home, ".volta", "bin"));
        }

        if (OperatingSystem.IsWindows())
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrEmpty(appData))
                directories.Add(Path.Combine(appData, "npm"));
        }
        else
        {
            directories.Add("/usr/local/bin");
            directories.Add("/opt/homebrew/bin");
        }

        return directories.Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>Makes the child process see the same extra folders (e.g. a node-based CLI needs node on PATH).</summary>
    public static void ExtendPath(IDictionary<string, string?> environment)
    {
        var key = environment.Keys.FirstOrDefault(name => string.Equals(name, "PATH", StringComparison.OrdinalIgnoreCase)) ?? "PATH";
        environment[key] = string.Join(Path.PathSeparator, GetSearchDirectories());
    }
}
