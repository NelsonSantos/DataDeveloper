namespace DataDeveloper.Data.Ai;

public sealed record AiProviderInfo(AiProviderType ProviderType, string DisplayName);

public class AiProviderFactoryService
{
    private readonly ICliProcessRunner _runner;
    private readonly string? _sessionsRoot;

    /// <param name="sessionsRoot">Folder under which each connection gets its own working folder for CLI sessions; null disables sessions.</param>
    public AiProviderFactoryService(ICliProcessRunner runner, string? sessionsRoot = null)
    {
        _runner = runner;
        _sessionsRoot = sessionsRoot;
    }

    /// <summary>The folder the CLI runs in for a connection's chats, like a project folder; null when sessions are not available.</summary>
    public string? GetSessionFolder(Guid connectionId) =>
        string.IsNullOrWhiteSpace(_sessionsRoot) ? null : Path.Combine(_sessionsRoot, connectionId.ToString("N"));

    public IReadOnlyList<AiProviderInfo> SupportedProviders { get; } =
    [
        new(AiProviderType.ClaudeCode, "Claude Code")
    ];

    public IAiProvider Create(AiProviderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return settings.ProviderType switch
        {
            AiProviderType.ClaudeCode => new ClaudeCodeAiProvider(_runner, settings),
            _ => throw new NotSupportedException($"AI provider {settings.ProviderType} is not supported.")
        };
    }
}
