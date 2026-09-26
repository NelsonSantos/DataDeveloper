using System;
using System.IO;
using DataDeveloper.Core;
using Xunit;
using static DataDeveloper.Core.AppDataLocation;

namespace DataDeveloper.Tests;

public class AppDataLocationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "datadeveloper-appdata-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Theory]
    [InlineData(Platform.MacOS, null, "/Users/ana/Library/Application Support/DataDeveloper")]
    [InlineData(Platform.Linux, null, "/Users/ana/.config/DataDeveloper")]
    [InlineData(Platform.Linux, "/Users/ana/.xdg", "/Users/ana/.xdg/DataDeveloper")]
    [InlineData(Platform.Linux, "relative/xdg", "/Users/ana/.config/DataDeveloper")]
    [InlineData(Platform.Windows, null, "/AppData/Roaming/DataDeveloper")]
    public void GetAppDataPath_UsesThePlatformsConfigurationFolder(Platform platform, string? xdgConfigHome, string expected)
    {
        var path = GetAppDataPath(platform, "/Users/ana", "/AppData/Roaming", xdgConfigHome);

        Assert.Equal(Path.GetFullPath(expected), Path.GetFullPath(path));
    }

    [Theory]
    [InlineData(Platform.MacOS, "/Users/ana/Documents/Library/Application Support/DataDeveloper")]
    [InlineData(Platform.Linux, "/Users/ana/Documents/.config/DataDeveloper")]
    public void GetLegacyAppDataPath_IsTheOldFolderUnderDocuments(Platform platform, string expected)
    {
        Assert.Equal(Path.GetFullPath(expected), Path.GetFullPath(GetLegacyAppDataPath(platform, "/Users/ana/Documents")!));
    }

    [Fact]
    public void GetLegacyAppDataPath_IsNullOnWindows()
    {
        Assert.Null(GetLegacyAppDataPath(Platform.Windows, @"C:\Users\ana\Documents"));
    }

    [Fact]
    public void MigrateLegacyData_MovesTheOldFolder_WhenTheNewOneDoesNotExist()
    {
        var legacy = CreateLegacyData();
        var target = Path.Combine(_root, "new", AppFolderName);

        var outcome = MigrateLegacyData(legacy, target);

        Assert.Equal(MigrationOutcome.Moved, outcome);
        Assert.False(Directory.Exists(legacy));
        Assert.Equal("connections", File.ReadAllText(Path.Combine(target, "state", "DataDeveloper.db")));
        Assert.Equal("{}", File.ReadAllText(Path.Combine(target, "Config", "recent-files.json")));
    }

    [Fact]
    public void MigrateLegacyData_MovesIntoAnEmptyNewFolder()
    {
        var legacy = CreateLegacyData();
        var target = Path.Combine(_root, "new", AppFolderName);
        Directory.CreateDirectory(Path.Combine(target, "logs"));

        var outcome = MigrateLegacyData(legacy, target);

        Assert.Equal(MigrationOutcome.Moved, outcome);
        Assert.True(File.Exists(Path.Combine(target, "state", "DataDeveloper.db")));
    }

    [Fact]
    public void MigrateLegacyData_LeavesBothFolders_WhenTheNewOneAlreadyHasData()
    {
        var legacy = CreateLegacyData();
        var target = Path.Combine(_root, "new", AppFolderName);
        Directory.CreateDirectory(Path.Combine(target, "state"));
        File.WriteAllText(Path.Combine(target, "state", "DataDeveloper.db"), "current");

        var outcome = MigrateLegacyData(legacy, target);

        Assert.Equal(MigrationOutcome.NotNeeded, outcome);
        Assert.Equal("current", File.ReadAllText(Path.Combine(target, "state", "DataDeveloper.db")));
        Assert.True(File.Exists(Path.Combine(legacy, "state", "DataDeveloper.db")));
    }

    [Fact]
    public void MigrateLegacyData_DoesNothing_WithoutOldData()
    {
        var target = Path.Combine(_root, "new", AppFolderName);

        Assert.Equal(MigrationOutcome.NotNeeded, MigrateLegacyData(Path.Combine(_root, "missing"), target));
        Assert.Equal(MigrationOutcome.NotNeeded, MigrateLegacyData(null, target));
        Assert.False(Directory.Exists(target));
    }

    [Fact]
    public void MigrateLegacyData_DoesNothing_WhenBothPathsAreTheSame()
    {
        var legacy = CreateLegacyData();

        Assert.Equal(MigrationOutcome.NotNeeded, MigrateLegacyData(legacy, legacy + Path.DirectorySeparatorChar));
        Assert.True(File.Exists(Path.Combine(legacy, "state", "DataDeveloper.db")));
    }

    private string CreateLegacyData()
    {
        var legacy = Path.Combine(_root, "Documents", "Library", "Application Support", AppFolderName);
        Directory.CreateDirectory(Path.Combine(legacy, "state"));
        Directory.CreateDirectory(Path.Combine(legacy, "Config"));
        File.WriteAllText(Path.Combine(legacy, "state", "DataDeveloper.db"), "connections");
        File.WriteAllText(Path.Combine(legacy, "Config", "recent-files.json"), "{}");
        return legacy;
    }
}
