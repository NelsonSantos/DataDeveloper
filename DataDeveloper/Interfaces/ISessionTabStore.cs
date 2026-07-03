using System;
using System.Collections.Generic;
using DataDeveloper.Models;

namespace DataDeveloper.Interfaces;

public interface ISessionTabStore
{
    ConnectionSessionState? Get(Guid connectionId);
    void Save(Guid connectionId, IReadOnlyList<EditorTabState> editors);
    void Remove(Guid connectionId);
}
