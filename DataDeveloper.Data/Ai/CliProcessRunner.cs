using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace DataDeveloper.Data.Ai;

public sealed class CliProcessRunner : ICliProcessRunner
{
    public async Task<CliProcessResult> RunAsync(CliProcessRequest request, CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = request.FileName,
            WorkingDirectory = request.WorkingDirectory ?? string.Empty,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        // ArgumentList (never a joined string) so prompt text can't be interpreted by a shell.
        foreach (var argument in request.Arguments)
            startInfo.ArgumentList.Add(argument);

        CliExecutableLocator.ExtendPath(startInfo.Environment);

        using var process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            return new CliProcessResult(false, -1, string.Empty, string.Empty, false, exception.Message);
        }

        using var timeoutSource = new CancellationTokenSource(request.Timeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);

        var outputTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var errorTask = process.StandardError.ReadToEndAsync(CancellationToken.None);

        try
        {
            await WriteStandardInputAsync(process, request.StandardInput);
            await process.WaitForExitAsync(linkedSource.Token);
        }
        catch (OperationCanceledException)
        {
            // A CLI waiting for input that never comes would hang forever: always kill the whole tree.
            TryKill(process);
            await process.WaitForExitAsync(CancellationToken.None);

            if (cancellationToken.IsCancellationRequested)
                throw;

            return new CliProcessResult(true, -1, await outputTask, await errorTask, true);
        }

        return new CliProcessResult(true, process.ExitCode, await outputTask, await errorTask, false);
    }

    private static async Task WriteStandardInputAsync(Process process, string? standardInput)
    {
        try
        {
            if (!string.IsNullOrEmpty(standardInput))
                await process.StandardInput.WriteAsync(standardInput);

            process.StandardInput.Close();
        }
        catch (IOException)
        {
            // The process exited before reading its input; the exit code and stderr explain why.
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
    }
}
