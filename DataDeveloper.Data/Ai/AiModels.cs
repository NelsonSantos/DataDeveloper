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

public sealed record AiChatResponse(bool Success, string Content, string? ErrorMessage = null)
{
    public static AiChatResponse Ok(string content) => new(true, content);

    public static AiChatResponse Fail(string errorMessage) => new(false, string.Empty, errorMessage);
}

public sealed record AiProviderAvailability(bool IsAvailable, string? ExecutablePath, string? Version, string? Detail);
