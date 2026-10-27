namespace DataDeveloper.Data.Ai;

public enum AiChatRole
{
    User = 1,
    Assistant = 2
}

public sealed record AiChatMessage(AiChatRole Role, string Content, DateTime CreatedAtUtc);

/// <param name="History">Earlier turns of the conversation, oldest first, not including <paramref name="Prompt"/>.</param>
public sealed record AiChatRequest(string Prompt, string? SystemPrompt = null, IReadOnlyList<AiChatMessage>? History = null);

public sealed record AiChatResponse(bool Success, string Content, string? ErrorMessage = null)
{
    public static AiChatResponse Ok(string content) => new(true, content);

    public static AiChatResponse Fail(string errorMessage) => new(false, string.Empty, errorMessage);
}

public sealed record AiProviderAvailability(bool IsAvailable, string? ExecutablePath, string? Version, string? Detail);
