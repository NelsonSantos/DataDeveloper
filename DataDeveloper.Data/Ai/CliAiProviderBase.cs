namespace DataDeveloper.Data.Ai;

/// <summary>
/// Talks to an AI vendor's official command line tool in its non-interactive mode. Subclasses only decide the
/// arguments and how to read the output; process handling, timeout and error mapping live here so a CLI that
/// changes its flags or output fails visibly instead of hanging or crashing the app.
/// </summary>
public abstract class CliAiProviderBase : IAiProvider
{
    private static readonly TimeSpan AvailabilityTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan SignInTimeout = TimeSpan.FromMinutes(5);

    private readonly ICliProcessRunner _runner;

    protected CliAiProviderBase(ICliProcessRunner runner, AiProviderSettings settings)
    {
        _runner = runner;
        Settings = settings;
    }

    protected AiProviderSettings Settings { get; }

    public abstract AiProviderType ProviderType { get; }
    public abstract string DisplayName { get; }

    protected abstract string CommandName { get; }
    protected abstract IReadOnlyList<string> BuildArguments(AiChatRequest request, AiSessionMode mode);
    protected abstract AiChatResponse ParseOutput(string standardOutput);

    /// <summary>True when the CLI can keep a conversation between calls (resume a session by id).</summary>
    protected virtual bool SupportsSessions => false;

    /// <summary>True when the failure means the CLI has no session with the id that was asked to resume.</summary>
    protected virtual bool IsMissingSession(AiChatResponse response) => false;

    /// <summary>True when the failure means a session with the id to create already exists.</summary>
    protected virtual bool IsSessionInUse(AiChatResponse response) => false;

    public virtual void ForgetSession(Guid sessionId)
    {
    }

    protected virtual IReadOnlyList<string> VersionArguments => ["--version"];

    /// <summary>Arguments of the CLI's own sign-in command, or null when it has none.</summary>
    protected virtual IReadOnlyList<string>? LoginArguments => null;

    public bool SupportsSignIn => LoginArguments is not null;

    /// <summary>
    /// The prompt goes through stdin so its size and content never reach the command line. A resumed session already
    /// holds the conversation, so only the new message is sent; otherwise the earlier turns go with it.
    /// </summary>
    protected virtual string? BuildStandardInput(AiChatRequest request, AiSessionMode mode) => mode switch
    {
        AiSessionMode.Resume => request.Prompt,
        AiSessionMode.Create => AiTranscriptFormatter.Format(request with { Prompt = request.FreshPrompt ?? request.Prompt }),
        _ => AiTranscriptFormatter.Format(request)
    };

    public async Task<AiProviderAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        var executable = CliExecutableLocator.Locate(CommandName, Settings.ExecutablePath);
        if (executable is null)
            return new AiProviderAvailability(false, null, null, NotFoundMessage());

        var result = await RunAsync(executable, VersionArguments, null, AvailabilityTimeout, cancellationToken);
        if (!result.Started)
            return new AiProviderAvailability(false, executable, null, result.StartError);

        if (result.TimedOut || result.ExitCode != 0)
            return new AiProviderAvailability(false, executable, null, DescribeFailure(result, AvailabilityTimeout));

        var version = result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        return new AiProviderAvailability(true, executable, version, null);
    }

    public async Task<AiChatResponse> SignInAsync(CancellationToken cancellationToken = default)
    {
        var loginArguments = LoginArguments;
        if (loginArguments is null)
            return AiChatResponse.Fail($"{DisplayName} must be signed in from its own command line tool.");

        var executable = CliExecutableLocator.Locate(CommandName, Settings.ExecutablePath);
        if (executable is null)
            return AiChatResponse.Fail(NotFoundMessage());

        var result = await RunAsync(executable, loginArguments, null, SignInTimeout, cancellationToken);

        if (!result.Started)
            return AiChatResponse.Fail(result.StartError ?? $"{DisplayName} could not be started.");

        if (result.TimedOut)
            return AiChatResponse.Fail("Sign in was not completed within 5 minutes.");

        if (result.ExitCode != 0)
            return AiChatResponse.Fail(DescribeFailure(result, SignInTimeout));

        var message = result.StandardOutput.Trim();
        return AiChatResponse.Ok(message.Length > 0 ? message : "Signed in.");
    }

    public async Task<AiChatResponse> SendMessageAsync(AiChatRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var executable = CliExecutableLocator.Locate(CommandName, Settings.ExecutablePath);
        if (executable is null)
            return AiChatResponse.Fail(NotFoundMessage());

        var timeout = TimeSpan.FromSeconds(AiProviderSettings.NormalizeTimeout(Settings.TimeoutSeconds));

        if (request.Session is not { } session || !SupportsSessions)
            return await RunChatAsync(executable, request, AiSessionMode.None, timeout, cancellationToken);

        if (session.Started)
        {
            var resumed = await RunChatAsync(executable, request, AiSessionMode.Resume, timeout, cancellationToken);
            if (resumed.Success || !IsMissingSession(resumed))
                return resumed;

            // The CLI lost the session (cleaned up, moved): start a new one that is told the whole conversation again.
        }

        var created = await RunChatAsync(executable, request, AiSessionMode.Create, timeout, cancellationToken);
        if (!created.Success && IsSessionInUse(created))
        {
            // Our records said there was no session but the CLI has one (an earlier call died half way): replace it.
            ForgetSession(session.Id);
            created = await RunChatAsync(executable, request, AiSessionMode.Create, timeout, cancellationToken);
        }

        return created;
    }

    private async Task<AiChatResponse> RunChatAsync(
        string executable,
        AiChatRequest request,
        AiSessionMode mode,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var workingDirectory = mode == AiSessionMode.None ? null : request.Session?.WorkingDirectory;
        var result = await RunAsync(executable, BuildArguments(request, mode), BuildStandardInput(request, mode), timeout, cancellationToken, workingDirectory);

        if (!result.Started)
            return AiChatResponse.Fail(result.StartError ?? $"{DisplayName} could not be started.");

        if (result.TimedOut)
            return AiChatResponse.Fail($"{DisplayName} did not answer within {timeout.TotalSeconds:0} seconds.");

        if (result.ExitCode != 0)
        {
            // Prefer the CLI's own structured error (e.g. "Failed to authenticate"); otherwise show stderr/exit code.
            var parsedError = string.IsNullOrWhiteSpace(result.StandardOutput) ? null : ParseOutput(result.StandardOutput);
            return parsedError is { Success: false } ? parsedError : AiChatResponse.Fail(DescribeFailure(result, timeout));
        }

        return ParseOutput(result.StandardOutput);
    }

    private Task<CliProcessResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string? standardInput,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        string? workingDirectory = null)
    {
        var fileName = executable;
        var processArguments = arguments;

        // Windows can't start npm's .cmd/.bat shims directly.
        if (OperatingSystem.IsWindows() &&
            (executable.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ||
             executable.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)))
        {
            fileName = "cmd.exe";
            processArguments = ["/c", executable, .. arguments];
        }

        // A neutral folder keeps the CLI from picking up project files (e.g. CLAUDE.md) from the app's folder. A session's
        // own folder is neutral too, and it is what keeps the CLI's stored sessions apart from the user's projects.
        var folder = Path.GetTempPath();
        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            Directory.CreateDirectory(workingDirectory);
            folder = workingDirectory;
        }

        return _runner.RunAsync(
            new CliProcessRequest(fileName, processArguments, standardInput, folder, timeout),
            cancellationToken);
    }

    private string NotFoundMessage() =>
        $"{DisplayName} was not found. Install it, or set its executable path in Preferences.";

    private string DescribeFailure(CliProcessResult result, TimeSpan timeout)
    {
        if (result.TimedOut)
            return $"{DisplayName} did not answer within {timeout.TotalSeconds:0} seconds.";

        var details = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
        details = details.Trim();
        if (details.Length > 500)
            details = details[..500] + "...";

        return string.IsNullOrEmpty(details)
            ? $"{DisplayName} exited with code {result.ExitCode}."
            : $"{DisplayName} exited with code {result.ExitCode}: {details}";
    }
}
