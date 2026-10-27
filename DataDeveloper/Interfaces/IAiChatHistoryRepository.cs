using System;
using System.Collections.Generic;
using DataDeveloper.Data.Ai;

namespace DataDeveloper.Interfaces;

public interface IAiChatHistoryRepository
{
    /// <summary>The most recent messages of a connection's chat, oldest first.</summary>
    IReadOnlyList<AiChatMessage> Load(Guid connectionId, int limit = 200);

    void Append(Guid connectionId, AiChatMessage message);

    void Clear(Guid connectionId);
}
