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
    int MessageCount);

/// <param name="History">Earlier turns of the conversation, oldest first, not including <paramref name="Prompt"/>.</param>
public sealed record AiChatRequest(string Prompt, string? SystemPrompt = null, IReadOnlyList<AiChatMessage>? History = null);

/// <summary>What one CLI call consumed. Input counts everything the model read, cached or not.</summary>
/// <param name="CostUsd">API-equivalent price reported by the CLI; subscriptions are not billed for it but it is a fair relative weight.</param>
public sealed record AiUsage(
    long InputTokens,
    long CacheReadTokens,
    long CacheCreationTokens,
    long OutputTokens,
    decimal? CostUsd = null,
    string? Model = null)
{
    public long TotalInputTokens => InputTokens + CacheReadTokens + CacheCreationTokens;

    public AiUsage Add(AiUsage other) => new(
        InputTokens + other.InputTokens,
        CacheReadTokens + other.CacheReadTokens,
        CacheCreationTokens + other.CacheCreationTokens,
        OutputTokens + other.OutputTokens,
        CostUsd is null && other.CostUsd is null ? null : (CostUsd ?? 0) + (other.CostUsd ?? 0),
        Model == other.Model ? Model : null);
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
