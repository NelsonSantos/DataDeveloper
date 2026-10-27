using DataDeveloper.Data.Ai;
using Xunit;

namespace DataDeveloper.Tests.Ai;

public class CliProcessRunnerTests
{
    private readonly CliProcessRunner _runner = new();

    [Fact]
    public async Task RunAsync_ReportsMissingExecutable()
    {
        var result = await _runner.RunAsync(new CliProcessRequest("/definitely/not/here", [], null, null, TimeSpan.FromSeconds(5)));

        Assert.False(result.Started);
        Assert.NotNull(result.StartError);
    }

    [Fact]
    public async Task RunAsync_CapturesStdoutAndPassesStdinAndArgumentsUntouched()
    {
        if (OperatingSystem.IsWindows())
            return;

        var result = await _runner.RunAsync(new CliProcessRequest("/bin/sh", ["-c", "cat; echo \"$0\"", "a b;c"], "from stdin", null, TimeSpan.FromSeconds(10)));

        Assert.True(result.Started);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("from stdina b;c\n", result.StandardOutput);
    }

    [Fact]
    public async Task RunAsync_KillsProcessThatExceedsTheTimeout()
    {
        if (OperatingSystem.IsWindows())
            return;

        var result = await _runner.RunAsync(new CliProcessRequest("/bin/sh", ["-c", "sleep 30"], null, null, TimeSpan.FromMilliseconds(300)));

        Assert.True(result.TimedOut);
    }

    [Fact]
    public async Task RunAsync_ThrowsWhenCallerCancels()
    {
        if (OperatingSystem.IsWindows())
            return;

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _runner.RunAsync(new CliProcessRequest("/bin/sh", ["-c", "sleep 30"], null, null, TimeSpan.FromSeconds(60)), cancellation.Token));
    }
}
