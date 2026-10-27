namespace DataDeveloper.Data.Ai;

public sealed record AiProviderInfo(AiProviderType ProviderType, string DisplayName);

public class AiProviderFactoryService
{
    private readonly ICliProcessRunner _runner;

    public AiProviderFactoryService(ICliProcessRunner runner)
    {
        _runner = runner;
    }

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
