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

    protected override IReadOnlyList<string> BuildArguments(AiChatRequest request)
    {
        var arguments = new List<string>
        {
            "-p",
            "--output-format", "json",
            "--tools", "",
            "--no-session-persistence",
            "--disable-slash-commands"
        };

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

    protected override AiChatResponse ParseOutput(string standardOutput)
    {
        try
        {
            using var document = JsonDocument.Parse(standardOutput);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                var result = root.TryGetProperty("result", out var resultElement) && resultElement.ValueKind == JsonValueKind.String
                    ? resultElement.GetString()
                    : null;
                var isError = root.TryGetProperty("is_error", out var errorElement) && errorElement.ValueKind == JsonValueKind.True;

                if (isError)
                    return AiChatResponse.Fail(string.IsNullOrWhiteSpace(result) ? "Claude Code reported an error." : result.Trim());

                if (result is not null)
                    return AiChatResponse.Ok(result.Trim());
            }
        }
        catch (JsonException)
        {
            // Output format changed or the CLI printed plain text: fall back to the raw text below.
        }

        var raw = standardOutput.Trim();
        return raw.Length > 0
            ? AiChatResponse.Ok(raw)
            : AiChatResponse.Fail("Claude Code returned an empty answer.");
    }
}
