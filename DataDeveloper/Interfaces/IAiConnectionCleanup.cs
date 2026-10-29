using System;

namespace DataDeveloper.Interfaces;

/// <summary>Removes what the AI assistant keeps for a connection once the connection itself is deleted.</summary>
public interface IAiConnectionCleanup
{
    void Forget(Guid connectionId);
}
