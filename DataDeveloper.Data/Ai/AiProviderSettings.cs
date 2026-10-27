namespace DataDeveloper.Data.Ai;

public sealed class AiProviderSettings
{
    public const int DefaultTimeoutSeconds = 120;
    public const int MinTimeoutSeconds = 10;
    public const int MaxTimeoutSeconds = 600;

    public AiProviderType ProviderType { get; set; } = AiProviderType.ClaudeCode;

    /// <summary>Optional explicit path to the CLI; when empty the executable is located automatically.</summary>
    public string? ExecutablePath { get; set; }

    /// <summary>Optional model name/alias passed to the CLI; when empty the CLI default is used.</summary>
    public string? Model { get; set; }

    public int TimeoutSeconds { get; set; } = DefaultTimeoutSeconds;

    public static int NormalizeTimeout(int seconds) =>
        Math.Clamp(seconds, MinTimeoutSeconds, MaxTimeoutSeconds);
}
