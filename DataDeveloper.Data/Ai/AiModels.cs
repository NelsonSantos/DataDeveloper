namespace DataDeveloper.Data.Ai;

public sealed record AiChatRequest(string Prompt, string? SystemPrompt = null);

public sealed record AiChatResponse(bool Success, string Content, string? ErrorMessage = null)
{
    public static AiChatResponse Ok(string content) => new(true, content);

    public static AiChatResponse Fail(string errorMessage) => new(false, string.Empty, errorMessage);
}

public sealed record AiProviderAvailability(bool IsAvailable, string? ExecutablePath, string? Version, string? Detail);
