namespace DataDeveloper.Core;

/// <summary>
/// Where the app keeps its data (settings database, sessions, logs, config) on each platform, and the one-time move
/// from the folder earlier versions used by mistake on macOS and Linux: they built the path from
/// <c>SpecialFolder.Personal</c>, which since .NET 8 is <c>~/Documents</c> there, not the home folder.
/// </summary>
public static class AppDataLocation
{
    public const string AppFolderName = "DataDeveloper";

    public enum Platform { Windows, MacOS, Linux }

    public enum MigrationOutcome
    {
        /// <summary>No data in the old folder, or the new folder already has data.</summary>
        NotNeeded,
        Moved,
        /// <summary>The folder could not be moved; its files were copied and the old folder was kept.</summary>
        Copied
    }

    public static Platform CurrentPlatform =>
        OperatingSystem.IsWindows() ? Platform.Windows : OperatingSystem.IsMacOS() ? Platform.MacOS : Platform.Linux;

    /// <summary>The app data folder: %AppData% on Windows, ~/Library/Application Support on macOS, and
    /// $XDG_CONFIG_HOME (or ~/.config) on Linux.</summary>
    public static string GetAppDataPath(Platform platform, string homePath, string applicationDataPath, string? xdgConfigHome)
    {
        var basePath = platform switch
        {
            Platform.Windows => applicationDataPath,
            Platform.MacOS => Path.Combine(homePath, "Library", "Application Support"),
            _ => !string.IsNullOrWhiteSpace(xdgConfigHome) && Path.IsPathRooted(xdgConfigHome)
                ? xdgConfigHome
                : Path.Combine(homePath, ".config")
        };

        return Path.Combine(basePath, AppFolderName);
    }

    /// <summary>The folder earlier versions used on macOS and Linux (under the documents folder); null on Windows.</summary>
    public static string? GetLegacyAppDataPath(Platform platform, string documentsPath) => platform switch
    {
        Platform.MacOS => Path.Combine(documentsPath, "Library", "Application Support", AppFolderName),
        Platform.Linux => Path.Combine(documentsPath, ".config", AppFolderName),
        _ => null
    };

    /// <summary>
    /// Brings the data from <paramref name="legacyPath"/> to <paramref name="appDataPath"/> when only the old folder
    /// has data. Nothing is overwritten, and the old folder is only removed by a successful move.
    /// </summary>
    public static MigrationOutcome MigrateLegacyData(string? legacyPath, string appDataPath)
    {
        if (legacyPath is null ||
            PathsEqual(legacyPath, appDataPath) ||
            !HasFiles(legacyPath) ||
            HasFiles(appDataPath))
            return MigrationOutcome.NotNeeded;

        // An empty folder (e.g. created by an earlier run) would block the move.
        if (Directory.Exists(appDataPath))
            Directory.Delete(appDataPath, recursive: true);

        Directory.CreateDirectory(Path.GetDirectoryName(appDataPath)!);
        try
        {
            Directory.Move(legacyPath, appDataPath);
            return MigrationOutcome.Moved;
        }
        catch (IOException)
        {
            // Different volumes (or a folder in use) cannot be renamed; copy instead and keep the original.
            try
            {
                CopyWithoutOverwriting(legacyPath, appDataPath);
                return MigrationOutcome.Copied;
            }
            catch
            {
                // A half-copied folder would be taken as the app's data next time; it was empty before, so drop it.
                if (Directory.Exists(appDataPath))
                    Directory.Delete(appDataPath, recursive: true);
                throw;
            }
        }
    }

    private static void CopyWithoutOverwriting(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            var target = Path.Combine(destination, Path.GetFileName(file));
            if (!File.Exists(target))
                File.Copy(file, target);
        }

        foreach (var directory in Directory.EnumerateDirectories(source))
            CopyWithoutOverwriting(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }

    private static bool HasFiles(string path) =>
        Directory.Exists(path) && Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Any();

    private static bool PathsEqual(string left, string right) =>
        string.Equals(Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar), StringComparison.Ordinal);
}
