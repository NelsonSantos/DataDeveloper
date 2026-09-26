using System;
using System.Collections.Generic;

namespace DataDeveloper.Interfaces;

/// <summary>Stores the ids of the most recently opened connections, most recent first.</summary>
public interface IRecentConnectionsService
{
    IReadOnlyList<Guid> Load();
    void Save(IReadOnlyList<Guid> connectionIds);
}
