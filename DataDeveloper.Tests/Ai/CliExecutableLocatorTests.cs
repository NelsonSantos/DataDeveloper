using DataDeveloper.Data.Ai;
using Xunit;

namespace DataDeveloper.Tests.Ai;

public class CliExecutableLocatorTests
{
    [Fact]
    public void Locate_UsesExplicitPathOnlyWhenItExists()
    {
        Assert.Equal("/x/claude", CliExecutableLocator.Locate("claude", " /x/claude ", [], false, path => path == "/x/claude"));
        Assert.Null(CliExecutableLocator.Locate("claude", "/x/claude", ["/bin"], false, _ => false));
    }

    [Fact]
    public void Locate_ReturnsFirstDirectoryContainingTheCommand()
    {
        var found = CliExecutableLocator.Locate("claude", null, ["/a", "/b", "/c"], false,
            path => path == Path.Combine("/b", "claude") || path == Path.Combine("/c", "claude"));

        Assert.Equal(Path.Combine("/b", "claude"), found);
    }

    [Fact]
    public void Locate_OnWindowsPrefersExeThenCmd()
    {
        var found = CliExecutableLocator.Locate("claude", null, ["/a"], true,
            path => path.EndsWith("claude.cmd") || path.EndsWith("claude.exe"));

        Assert.EndsWith("claude.exe", found);
    }

    [Fact]
    public void Locate_ReturnsNullWhenNothingMatches()
    {
        Assert.Null(CliExecutableLocator.Locate("claude", null, ["/a"], false, _ => false));
    }

    [Fact]
    public void GetSearchDirectories_IncludesUserInstallFoldersWithoutDuplicates()
    {
        var directories = CliExecutableLocator.GetSearchDirectories();

        Assert.Contains(directories, d => d.EndsWith(Path.Combine(".local", "bin")));
        Assert.Equal(directories.Count, directories.Distinct().Count());
    }
}
