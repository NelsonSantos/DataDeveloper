using System.Text;

namespace DataDeveloper.Data.Ai;

/// <summary>
/// The CLIs answer one prompt per call, so a multi-turn chat is sent as a transcript of the earlier turns
/// followed by the new message.
/// </summary>
public static class AiTranscriptFormatter
{
    public const int MaxHistoryMessages = 12;
    public const int MaxHistoryCharacters = 24_000;

    /// <summary>Keeps the most recent turns that fit, so a long chat never outgrows the CLI's context.</summary>
    public static IReadOnlyList<AiChatMessage> TrimHistory(IReadOnlyList<AiChatMessage> history)
    {
        var kept = new List<AiChatMessage>();
        var characters = 0;

        for (var index = history.Count - 1; index >= 0 && kept.Count < MaxHistoryMessages; index--)
        {
            characters += history[index].Content.Length;
            if (characters > MaxHistoryCharacters && kept.Count > 0)
                break;

            kept.Add(history[index]);
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
            builder.AppendLine(message.Content);
            builder.AppendLine();
        }

        builder.AppendLine("Reply to the new message below, taking the conversation into account.");
        builder.AppendLine();
        builder.Append("User: ");
        builder.Append(request.Prompt);
        return builder.ToString();
    }
}
