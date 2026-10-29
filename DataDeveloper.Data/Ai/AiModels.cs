namespace DataDeveloper.Data.Ai;

public enum AiChatRole
{
    User = 1,
    Assistant = 2
}

/// <param name="Content">What the chat shows: the typed text, plus the names of whatever was attached.</param>
/// <param name="ModelContent">
/// What the model saw for this turn when it differs from <paramref name="Content"/> (the text with its attachments inlined),
/// so later turns still see attached files and queries. Null means <paramref name="Content"/> is all there was.
/// </param>
public sealed record AiChatMessage(AiChatRole Role, string Content, DateTime CreatedAtUtc, string? ModelContent = null);

/// <summary>One conversation of a connection's AI assistant (a "task"), as listed in the chat picker.</summary>
public sealed record AiChatInfo(
    Guid Id,
    Guid ConnectionId,
    string Title,
    string? Model,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    int MessageCount,
    bool SessionStarted = false);

/// <summary>
/// A conversation kept by the CLI itself, so each call sends only the new message and the CLI reuses what it already
/// read. The session id is the chat id.
/// </summary>
/// <param name="Started">True once the CLI has stored this session; false means the next call must create it.</param>
/// <param name="WorkingDirectory">Folder the CLI runs in, which is what keeps its sessions apart from the user's own projects.</param>
public sealed record AiSession(Guid Id, bool Started, string? WorkingDirectory);

/// <param name="Prompt">The new message. With a started session it is all that is sent.</param>
/// <param name="History">Earlier turns, oldest first, not including <paramref name="Prompt"/>; sent when there is no session to resume.</param>
/// <param name="Session">When set the call uses a CLI session; otherwise every call is independent and carries the history.</param>
/// <param name="FreshPrompt">
/// The new message as it must read when the CLI has no memory of the conversation (a session being created or lost):
/// it repeats context that a resumed session already has. Falls back to <paramref name="Prompt"/>.
/// </param>
public sealed record AiChatRequest(
    string Prompt,
    string? SystemPrompt = null,
    IReadOnlyList<AiChatMessage>? History = null,
    AiSession? Session = null,
    string? FreshPrompt = null);

/// <summary>What one CLI call consumed. Input counts everything the model read, cached or not.</summary>
/// <param name="CostUsd">API-equivalent price reported by the CLI; subscriptions are not billed for it but it is a fair relative weight.</param>
public sealed record AiUsage(
    long InputTokens,
    long CacheReadTokens,
    long CacheCreationTokens,
    long OutputTokens,
    decimal? CostUsd = null,
    string? Model = null,
    long? ContextWindow = null)
{
    public long TotalInputTokens => InputTokens + CacheReadTokens + CacheCreationTokens;

    /// <summary>How much of the model's context the call filled: everything it read plus what it wrote.</summary>
    public long ContextTokens => TotalInputTokens + OutputTokens;

    public AiUsage Add(AiUsage other) => new(
        InputTokens + other.InputTokens,
        CacheReadTokens + other.CacheReadTokens,
        CacheCreationTokens + other.CacheCreationTokens,
        OutputTokens + other.OutputTokens,
        CostUsd is null && other.CostUsd is null ? null : (CostUsd ?? 0) + (other.CostUsd ?? 0),
        Model == other.Model ? Model : null,
        other.ContextWindow ?? ContextWindow);
}

/// <summary>State of one of the account's usage windows as reported by the CLI (for example the 5 hour session).</summary>
/// <param name="Status">"allowed" while there is room; anything else means the window is limited or close to it.</param>
/// <param name="WindowType">The CLI's name for the window, such as "five_hour" or "seven_day".</param>
public sealed record AiRateLimit(string Status, DateTime? ResetsAtUtc, string? WindowType, bool IsUsingOverage = false)
{
    public bool IsAllowed => string.Equals(Status, "allowed", StringComparison.OrdinalIgnoreCase);
}

public sealed record AiChatResponse(
    bool Success,
    string Content,
    string? ErrorMessage = null,
    AiUsage? Usage = null,
    IReadOnlyList<AiRateLimit>? RateLimits = null)
{
    public static AiChatResponse Ok(string content, AiUsage? usage = null, IReadOnlyList<AiRateLimit>? rateLimits = null) =>
        new(true, content, null, usage, rateLimits);

    public static AiChatResponse Fail(string errorMessage, IReadOnlyList<AiRateLimit>? rateLimits = null) =>
        new(false, string.Empty, errorMessage, null, rateLimits);
}

public sealed record AiProviderAvailability(bool IsAvailable, string? ExecutablePath, string? Version, string? Detail);
