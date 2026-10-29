using DataDeveloper.Data.Ai;
using Xunit;

namespace DataDeveloper.Tests.Ai;

public class ClaudeCodeAiProviderTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("ai-tests").FullName;
    private readonly string _executable;

    public ClaudeCodeAiProviderTests()
    {
        _executable = Path.Combine(_folder, "claude");
        File.WriteAllText(_executable, string.Empty);
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private ClaudeCodeAiProvider CreateProvider(FakeRunner runner, Action<AiProviderSettings>? configure = null)
    {
        var settings = new AiProviderSettings { ExecutablePath = _executable };
        configure?.Invoke(settings);
        return new ClaudeCodeAiProvider(runner, settings);
    }

    [Fact]
    public async Task SendMessage_ReadsResultFromJsonAndSendsPromptThroughStdin()
    {
        var runner = new FakeRunner(new CliProcessResult(true, 0, """{"is_error":false,"result":" OK \n"}""", string.Empty, false));

        var response = await CreateProvider(runner).SendMessageAsync(new AiChatRequest("hello; rm -rf /"));

        Assert.True(response.Success);
        Assert.Equal("OK", response.Content);
        Assert.Equal("hello; rm -rf /", runner.LastRequest!.StandardInput);
        Assert.DoesNotContain("hello; rm -rf /", runner.LastRequest.Arguments);
    }

    [Fact]
    public async Task SendMessage_DisablesToolsAndSessionPersistence()
    {
        var runner = new FakeRunner(new CliProcessResult(true, 0, """{"result":"x"}""", string.Empty, false));

        await CreateProvider(runner).SendMessageAsync(new AiChatRequest("hi"));

        var arguments = runner.LastRequest!.Arguments;
        Assert.Contains("-p", arguments);
        Assert.Equal("stream-json", arguments[arguments.ToList().IndexOf("--output-format") + 1]);
        Assert.Contains("--verbose", arguments);
        Assert.Equal(string.Empty, arguments[arguments.ToList().IndexOf("--tools") + 1]);
        Assert.Contains("--no-session-persistence", arguments);
    }

    [Fact]
    public async Task SendMessage_PassesModelAndSystemPromptWhenProvided()
    {
        var runner = new FakeRunner(new CliProcessResult(true, 0, """{"result":"x"}""", string.Empty, false));

        await CreateProvider(runner, s => s.Model = " sonnet ")
            .SendMessageAsync(new AiChatRequest("hi", "be brief"));

        var arguments = runner.LastRequest!.Arguments.ToList();
        Assert.Equal("sonnet", arguments[arguments.IndexOf("--model") + 1]);
        Assert.Equal("be brief", arguments[arguments.IndexOf("--system-prompt") + 1]);
    }

    [Fact]
    public async Task SendMessage_ReportsCliErrorFlag()
    {
        var runner = new FakeRunner(new CliProcessResult(true, 1, """{"is_error":true,"result":"Failed to authenticate"}""", string.Empty, false));

        var response = await CreateProvider(runner).SendMessageAsync(new AiChatRequest("hi"));

        Assert.False(response.Success);
        Assert.Equal("Failed to authenticate", response.ErrorMessage);
    }

    [Fact]
    public async Task SendMessage_FallsBackToRawTextWhenOutputIsNotJson()
    {
        var runner = new FakeRunner(new CliProcessResult(true, 0, "plain answer\n", string.Empty, false));

        var response = await CreateProvider(runner).SendMessageAsync(new AiChatRequest("hi"));

        Assert.True(response.Success);
        Assert.Equal("plain answer", response.Content);
    }

    [Fact]
    public async Task SendMessage_NonZeroExitWithoutJsonUsesStandardError()
    {
        var runner = new FakeRunner(new CliProcessResult(true, 2, string.Empty, "error: unknown option '--tools'", false));

        var response = await CreateProvider(runner).SendMessageAsync(new AiChatRequest("hi"));

        Assert.False(response.Success);
        Assert.Contains("unknown option", response.ErrorMessage);
    }

    [Fact]
    public async Task SendMessage_NonZeroExitNeverSucceedsEvenWithPlainStdout()
    {
        var runner = new FakeRunner(new CliProcessResult(true, 1, "some text", string.Empty, false));

        var response = await CreateProvider(runner).SendMessageAsync(new AiChatRequest("hi"));

        Assert.False(response.Success);
    }

    [Fact]
    public async Task SendMessage_ReportsTimeout()
    {
        var runner = new FakeRunner(new CliProcessResult(true, -1, string.Empty, string.Empty, true));

        var response = await CreateProvider(runner, s => s.TimeoutSeconds = 30).SendMessageAsync(new AiChatRequest("hi"));

        Assert.False(response.Success);
        Assert.Contains("30 seconds", response.ErrorMessage);
        Assert.Equal(TimeSpan.FromSeconds(30), runner.LastRequest!.Timeout);
    }

    [Fact]
    public async Task SendMessage_ClampsTimeout()
    {
        var runner = new FakeRunner(new CliProcessResult(true, 0, """{"result":"x"}""", string.Empty, false));

        await CreateProvider(runner, s => s.TimeoutSeconds = 100000).SendMessageAsync(new AiChatRequest("hi"));

        Assert.Equal(TimeSpan.FromSeconds(AiProviderSettings.MaxTimeoutSeconds), runner.LastRequest!.Timeout);
    }

    [Fact]
    public async Task SendMessage_ReportsMissingExecutableWithoutRunningAnything()
    {
        var runner = new FakeRunner(new CliProcessResult(true, 0, string.Empty, string.Empty, false));
        var provider = new ClaudeCodeAiProvider(runner, new AiProviderSettings { ExecutablePath = Path.Combine(_folder, "missing") });

        var response = await provider.SendMessageAsync(new AiChatRequest("hi"));

        Assert.False(response.Success);
        Assert.Contains("not found", response.ErrorMessage);
        Assert.Null(runner.LastRequest);
    }

    [Fact]
    public async Task SendMessage_ReportsProcessStartFailure()
    {
        var runner = new FakeRunner(new CliProcessResult(false, -1, string.Empty, string.Empty, false, "Permission denied"));

        var response = await CreateProvider(runner).SendMessageAsync(new AiChatRequest("hi"));

        Assert.False(response.Success);
        Assert.Equal("Permission denied", response.ErrorMessage);
    }

    [Fact]
    public async Task CheckAvailability_ReturnsVersion()
    {
        var runner = new FakeRunner(new CliProcessResult(true, 0, "2.1.0 (Claude Code)\n", string.Empty, false));

        var availability = await CreateProvider(runner).CheckAvailabilityAsync();

        Assert.True(availability.IsAvailable);
        Assert.Equal("2.1.0 (Claude Code)", availability.Version);
        Assert.Equal(_executable, availability.ExecutablePath);
        Assert.Equal(["--version"], runner.LastRequest!.Arguments);
    }

    [Fact]
    public async Task CheckAvailability_FailsWhenVersionCommandFails()
    {
        var runner = new FakeRunner(new CliProcessResult(true, 1, string.Empty, "boom", false));

        var availability = await CreateProvider(runner).CheckAvailabilityAsync();

        Assert.False(availability.IsAvailable);
        Assert.Contains("boom", availability.Detail);
    }

    [Fact]
    public async Task SignIn_RunsTheCliLoginCommand()
    {
        var runner = new FakeRunner(new CliProcessResult(true, 0, "Login successful\n", string.Empty, false));
        var provider = CreateProvider(runner);

        var response = await provider.SignInAsync();

        Assert.True(provider.SupportsSignIn);
        Assert.True(response.Success);
        Assert.Equal(["auth", "login", "--claudeai"], runner.LastRequest!.Arguments);
        Assert.Equal(TimeSpan.FromMinutes(5), runner.LastRequest.Timeout);
    }

    [Fact]
    public async Task SignIn_ReportsTimeoutAndFailure()
    {
        var timedOut = await CreateProvider(new FakeRunner(new CliProcessResult(true, -1, string.Empty, string.Empty, true))).SignInAsync();
        var failed = await CreateProvider(new FakeRunner(new CliProcessResult(true, 1, string.Empty, "denied", false))).SignInAsync();

        Assert.False(timedOut.Success);
        Assert.Contains("5 minutes", timedOut.ErrorMessage);
        Assert.False(failed.Success);
        Assert.Contains("denied", failed.ErrorMessage);
    }

    // Output of `claude -p "oi" --output-format stream-json --verbose`, trimmed to the events the app reads.
    private const string StreamOutput = """
        {"type":"system","subtype":"init","session_id":"abc"}
        {"type":"rate_limit_event","rate_limit_info":{"status":"allowed","resetsAt":1793242800,"rateLimitType":"five_hour","overageStatus":"rejected","isUsingOverage":false}}
        {"type":"assistant","message":{"content":[{"type":"text","text":"Oi!"}]}}
        {"type":"result","subtype":"success","is_error":false,"total_cost_usd":0.0727602,"usage":{"input_tokens":2,"cache_creation_input_tokens":11248,"cache_read_input_tokens":16204,"output_tokens":27},"modelUsage":{"claude-sonnet-5":{"contextWindow":1000000}},"result":"Oi! Como posso ajudar?"}
        """;

    [Fact]
    public async Task SendMessage_ReadsAnswerUsageAndRateLimitFromStreamOutput()
    {
        var runner = new FakeRunner(new CliProcessResult(true, 0, StreamOutput, string.Empty, false));

        var response = await CreateProvider(runner).SendMessageAsync(new AiChatRequest("oi"));

        Assert.True(response.Success);
        Assert.Equal("Oi! Como posso ajudar?", response.Content);

        var usage = response.Usage!;
        Assert.Equal(2, usage.InputTokens);
        Assert.Equal(16204, usage.CacheReadTokens);
        Assert.Equal(11248, usage.CacheCreationTokens);
        Assert.Equal(27, usage.OutputTokens);
        Assert.Equal(27454, usage.TotalInputTokens);
        Assert.Equal(0.0727602m, usage.CostUsd);
        Assert.Equal("claude-sonnet-5", usage.Model);

        var limit = Assert.Single(response.RateLimits!);
        Assert.True(limit.IsAllowed);
        Assert.Equal("five_hour", limit.WindowType);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1793242800).UtcDateTime, limit.ResetsAtUtc);
        Assert.False(limit.IsUsingOverage);
    }

    [Fact]
    public async Task SendMessage_KeepsTheLatestStateOfEachUsageWindow()
    {
        const string output = """
            {"type":"rate_limit_event","rate_limit_info":{"status":"allowed","rateLimitType":"five_hour"}}
            {"type":"rate_limit_event","rate_limit_info":{"status":"allowed","rateLimitType":"seven_day"}}
            {"type":"rate_limit_event","rate_limit_info":{"status":"rejected","rateLimitType":"five_hour","isUsingOverage":true}}
            {"type":"result","is_error":false,"result":"x"}
            """;

        var response = await CreateProvider(new FakeRunner(new CliProcessResult(true, 0, output, string.Empty, false)))
            .SendMessageAsync(new AiChatRequest("hi"));

        Assert.Equal(2, response.RateLimits!.Count);
        var session = response.RateLimits.Single(l => l.WindowType == "five_hour");
        Assert.False(session.IsAllowed);
        Assert.True(session.IsUsingOverage);
        Assert.Null(response.Usage);
    }

    [Fact]
    public async Task SendMessage_ErrorResultStillCarriesTheRateLimit()
    {
        const string output = """
            {"type":"rate_limit_event","rate_limit_info":{"status":"rejected","rateLimitType":"five_hour"}}
            {"type":"result","is_error":true,"result":"You've hit your limit"}
            """;

        var response = await CreateProvider(new FakeRunner(new CliProcessResult(true, 1, output, string.Empty, false)))
            .SendMessageAsync(new AiChatRequest("hi"));

        Assert.False(response.Success);
        Assert.Equal("You've hit your limit", response.ErrorMessage);
        Assert.False(Assert.Single(response.RateLimits!).IsAllowed);
    }

    [Fact]
    public async Task SendMessage_IgnoresUnknownEventsAndBrokenLines()
    {
        const string output = "{\"type\":\"something_new\",\"x\":1}\n{not json\n{\"type\":\"result\",\"result\":\"ok\"}";

        var response = await CreateProvider(new FakeRunner(new CliProcessResult(true, 0, output, string.Empty, false)))
            .SendMessageAsync(new AiChatRequest("hi"));

        Assert.True(response.Success);
        Assert.Equal("ok", response.Content);
        Assert.Null(response.RateLimits);
    }

    [Fact]
    public async Task SendMessage_JsonWithoutAResultIsReportedInsteadOfShownAsAnswer()
    {
        var response = await CreateProvider(new FakeRunner(new CliProcessResult(true, 0, """{"type":"system"}""", string.Empty, false)))
            .SendMessageAsync(new AiChatRequest("hi"));

        Assert.False(response.Success);
        Assert.Contains("format", response.ErrorMessage);
    }

    private sealed class FakeRunner(CliProcessResult result) : ICliProcessRunner
    {
        public CliProcessRequest? LastRequest { get; private set; }

        public Task<CliProcessResult> RunAsync(CliProcessRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(result);
        }
    }
}
