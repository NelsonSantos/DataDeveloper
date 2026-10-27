namespace DataDeveloper.Data.Ai;

public sealed record CliProcessRequest(
    string FileName,
    IReadOnlyList<string> Arguments,
    string? StandardInput,
    string? WorkingDirectory,
    TimeSpan Timeout);

/// <param name="Started">False when the process could not be started at all (see <paramref name="StartError"/>).</param>
public sealed record CliProcessResult(
    bool Started,
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool TimedOut,
    string? StartError = null);

public interface ICliProcessRunner
{
    Task<CliProcessResult> RunAsync(CliProcessRequest request, CancellationToken cancellationToken = default);
}
