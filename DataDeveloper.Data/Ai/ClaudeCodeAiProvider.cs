using System.Text.Json;

namespace DataDeveloper.Data.Ai;

/// <summary>
/// Claude Code CLI in print mode (`claude -p`). Tools are disabled so the assistant can only answer in text,
/// never read files or run commands on the user's machine.
/// </summary>
public sealed class ClaudeCodeAiProvider : CliAiProviderBase
{
    public ClaudeCodeAiProvider(ICliProcessRunner runner, AiProviderSettings settings)
        : base(runner, settings)
    {
    }

    public override AiProviderType ProviderType => AiProviderType.ClaudeCode;
    public override string DisplayName => "Claude Code";

    protected override string CommandName => "claude";

    protected override IReadOnlyList<string>? LoginArguments => ["auth", "login", "--claudeai"];

    protected override bool SupportsSessions => true;

    protected override bool IsMissingSession(AiChatResponse response) =>
        response.ErrorMessage?.Contains("No conversation found", StringComparison.OrdinalIgnoreCase) == true;

    protected override bool IsSessionInUse(AiChatResponse response) =>
        response.ErrorMessage?.Contains("already in use", StringComparison.OrdinalIgnoreCase) == true;

    protected override IReadOnlyList<string> BuildArguments(AiChatRequest request, AiSessionMode mode)
    {
        var arguments = new List<string>
        {
            "-p",
            "--output-format", "stream-json",
            "--verbose",
            "--tools", "",
            "--disable-slash-commands"
        };

        switch (mode)
        {
            case AiSessionMode.Create when request.Session is { } created:
                arguments.Add("--session-id");
                arguments.Add(created.Id.ToString());
                break;
            case AiSessionMode.Resume when request.Session is { } resumed:
                arguments.Add("--resume");
                arguments.Add(resumed.Id.ToString());
                break;
            default:
                arguments.Add("--no-session-persistence");
                break;
        }

        if (!string.IsNullOrWhiteSpace(Settings.Model))
        {
            arguments.Add("--model");
            arguments.Add(Settings.Model.Trim());
        }

        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            arguments.Add("--system-prompt");
            arguments.Add(request.SystemPrompt);
        }

        return arguments;
    }

    /// <summary>Deletes the stored session, which the CLI keeps as <c>projects/&lt;folder&gt;/&lt;id&gt;.jsonl</c> in its config folder.</summary>
    public override void ForgetSession(Guid sessionId)
    {
        try
        {
            var configFolder = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
            if (string.IsNullOrWhiteSpace(configFolder))
                configFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");

            var projects = Path.Combine(configFolder, "projects");
            if (!Directory.Exists(projects))
                return;

            foreach (var project in Directory.EnumerateDirectories(projects))
            {
                var file = Path.Combine(project, sessionId + ".jsonl");
                if (File.Exists(file))
                    File.Delete(file);

                var folder = Path.Combine(project, sessionId.ToString());
                if (Directory.Exists(folder))
                    Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Cleaning up is best effort: a leftover session file is harmless and has an id nothing will resume.
        }
    }

    protected override AiChatResponse ParseOutput(string standardOutput)
    {
        // stream-json prints one JSON event per line; a single JSON object (the older `json` format) is the same with one line.
        JsonElement? resultEvent = null;
        var rateLimits = new Dictionary<string, AiRateLimit>(StringComparer.OrdinalIgnoreCase);
        var parsedAnyJson = false;

        foreach (var line in standardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line[0] != '{')
                continue;

            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement.Clone();
                if (root.ValueKind != JsonValueKind.Object)
                    continue;

                parsedAnyJson = true;
                var type = ReadString(root, "type");
                if (type == "rate_limit_event")
                {
                    if (ReadRateLimit(root) is { } limit)
                        rateLimits[limit.WindowType ?? string.Empty] = limit;
                }
                else if (type == "result" || (type is null && root.TryGetProperty("result", out _)))
                {
                    resultEvent = root;
                }
            }
            catch (JsonException)
            {
                // A line that is not JSON is ignored; the raw text fallback below covers output with no JSON at all.
            }
        }

        var limits = rateLimits.Count > 0 ? rateLimits.Values.ToList() : null;

        if (resultEvent is { } root1)
        {
            var result = ReadString(root1, "result");
            if (string.IsNullOrWhiteSpace(result) && root1.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
                result = string.Join("; ", errors.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()));

            var isError = root1.TryGetProperty("is_error", out var errorElement) && errorElement.ValueKind == JsonValueKind.True;

            if (isError)
                return AiChatResponse.Fail(string.IsNullOrWhiteSpace(result) ? "Claude Code reported an error." : result.Trim(), limits);

            if (result is not null)
                return AiChatResponse.Ok(result.Trim(), ReadUsage(root1), limits);
        }

        if (parsedAnyJson)
            return AiChatResponse.Fail("Claude Code returned an answer in a format this app does not understand.", limits);

        var raw = standardOutput.Trim();
        return raw.Length > 0
            ? AiChatResponse.Ok(raw)
            : AiChatResponse.Fail("Claude Code returned an empty answer.");
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static long ReadLong(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)
            ? number
            : 0;

    private static AiUsage? ReadUsage(JsonElement result)
    {
        if (!result.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object)
            return null;

        decimal? cost = result.TryGetProperty("total_cost_usd", out var costElement) && costElement.ValueKind == JsonValueKind.Number &&
                        costElement.TryGetDecimal(out var amount)
            ? amount
            : null;

        // The CLI may also run a small model for housekeeping (a summary of the turn), so modelUsage can list several
        // models; the one that did the most work is the one that answered.
        string? model = null;
        long? contextWindow = null;
        if (result.TryGetProperty("modelUsage", out var models) && models.ValueKind == JsonValueKind.Object)
        {
            var main = models.EnumerateObject()
                .Where(property => property.Value.ValueKind == JsonValueKind.Object)
                .OrderByDescending(property => ReadLong(property.Value, "inputTokens") + ReadLong(property.Value, "cacheReadInputTokens") +
                                               ReadLong(property.Value, "cacheCreationInputTokens") + ReadLong(property.Value, "outputTokens"))
                .Cast<JsonProperty?>()
                .FirstOrDefault();
            if (main is { } found)
            {
                model = found.Name;
                contextWindow = ReadLong(found.Value, "contextWindow") is > 0 and var window ? window : null;
            }
        }

        return new AiUsage(
            ReadLong(usage, "input_tokens"),
            ReadLong(usage, "cache_read_input_tokens"),
            ReadLong(usage, "cache_creation_input_tokens"),
            ReadLong(usage, "output_tokens"),
            cost,
            model,
            contextWindow);
    }

    private static AiRateLimit? ReadRateLimit(JsonElement eventElement)
    {
        if (!eventElement.TryGetProperty("rate_limit_info", out var info) || info.ValueKind != JsonValueKind.Object)
            return null;

        var status = ReadString(info, "status");
        if (status is null)
            return null;

        DateTime? resetsAt = info.TryGetProperty("resetsAt", out var resets) && resets.ValueKind == JsonValueKind.Number &&
                             resets.TryGetInt64(out var epochSeconds) && epochSeconds is > 0 and < 253_402_300_799
            ? DateTimeOffset.FromUnixTimeSeconds(epochSeconds).UtcDateTime
            : null;
        var usingOverage = info.TryGetProperty("isUsingOverage", out var overage) && overage.ValueKind == JsonValueKind.True;

        return new AiRateLimit(status, resetsAt, ReadString(info, "rateLimitType"), usingOverage);
    }
}
