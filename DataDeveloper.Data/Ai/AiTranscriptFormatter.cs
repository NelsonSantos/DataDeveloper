using System.Text;

namespace DataDeveloper.Data.Ai;

/// <summary>
/// The CLIs answer one prompt per call, so a multi-turn chat is sent as a transcript of the earlier turns
/// followed by the new message.
/// </summary>
public static class AiTranscriptFormatter
{
    public const int MaxHistoryMessages = 40;
    public const int MaxHistoryCharacters = 120_000;

    /// <summary>
    /// Keeps the most recent turns that fit, so a long chat never outgrows the CLI's context. A turn is kept with the
    /// text of its attachments while the budget allows; once it doesn't, older turns fall back to their short form
    /// (the typed text and the names of what was attached) before any turn is dropped.
    /// </summary>
    public static IReadOnlyList<AiChatMessage> TrimHistory(IReadOnlyList<AiChatMessage> history)
    {
        var kept = new List<AiChatMessage>();
        var characters = 0;

        for (var index = history.Count - 1; index >= 0 && kept.Count < MaxHistoryMessages; index--)
        {
            var message = history[index];
            var full = (message.ModelContent ?? message.Content).Length;

            if (characters + full <= MaxHistoryCharacters)
            {
                kept.Add(message);
                characters += full;
                continue;
            }

            if (message.ModelContent is not null && characters + message.Content.Length <= MaxHistoryCharacters)
            {
                kept.Add(message with { ModelContent = null });
                characters += message.Content.Length;
                continue;
            }

            // The newest turn is always sent, however big; anything older that doesn't fit ends the transcript.
            if (kept.Count == 0)
                kept.Add(message);

            break;
        }

        kept.Reverse();
        return kept;
    }

    public static string Format(AiChatRequest request)
    {
        if (request.History is not { Count: > 0 } history)
            return request.Prompt;

        var builder = new StringBuilder();
        builder.AppendLine("Conversation so far (oldest first):");
        builder.AppendLine();

        foreach (var message in history)
        {
            builder.Append(message.Role == AiChatRole.User ? "User: " : "Assistant: ");
            builder.AppendLine(message.ModelContent ?? message.Content);
            builder.AppendLine();
        }

        builder.AppendLine("Reply to the new message below, taking the conversation into account.");
        builder.AppendLine();
        builder.Append("User: ");
        builder.Append(request.Prompt);
        return builder.ToString();
    }
}
