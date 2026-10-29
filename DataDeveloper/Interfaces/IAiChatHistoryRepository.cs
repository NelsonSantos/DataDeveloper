using System.Linq;
using System;
using System.Collections.Generic;
using DataDeveloper.Data.Ai;

namespace DataDeveloper.Interfaces;

/// <summary>Conversations of the AI assistant. A connection can have several, each with its own messages and model.</summary>
public interface IAiChatHistoryRepository
{
    /// <summary>The connection's conversations, most recently active first.</summary>
    IReadOnlyList<AiChatInfo> ListChats(Guid connectionId);

    AiChatInfo CreateChat(Guid connectionId, string title = AiChatTitles.Default, string? model = null);

    void RenameChat(Guid chatId, string title);

    /// <summary>Remembers the model picked for the conversation; null or blank means "default".</summary>
    void SetChatModel(Guid chatId, string? model);

    /// <summary>Remembers whether the CLI holds a session for the conversation (so the next call can resume it).</summary>
    void SetSessionStarted(Guid chatId, bool started);

    /// <summary>Removes the conversation and its messages.</summary>
    void DeleteChat(Guid chatId);

    /// <summary>Removes every conversation of a connection (when the connection itself is deleted).</summary>
    void DeleteConnectionChats(Guid connectionId);

    /// <summary>The most recent messages of a conversation, oldest first.</summary>
    IReadOnlyList<AiChatMessage> LoadMessages(Guid chatId, int limit = 200);

    void Append(Guid chatId, AiChatMessage message);

    /// <summary>Removes the messages but keeps the conversation; the CLI session, which remembered them, is no longer valid.</summary>
    void Clear(Guid chatId);
}

public static class AiChatTitles
{
    public const string Default = "New chat";
    public const string Migrated = "Chat";
    public const int MaxLength = 60;

    /// <summary>A conversation title from the first thing typed: first line, trimmed and shortened.</summary>
    public static string FromText(string text)
    {
        var line = (text ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? string.Empty;
        if (line.Length == 0)
            return Default;

        return line.Length <= MaxLength ? line : line[..(MaxLength - 1)].TrimEnd() + "\u2026";
    }
}
