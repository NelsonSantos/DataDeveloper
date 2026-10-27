using System;
using System.Collections.Generic;
using DataDeveloper.Models;

namespace DataDeveloper.Interfaces;

public interface ISessionTabStore
{
    ConnectionSessionState? Get(Guid connectionId);
    void Save(Guid connectionId, IReadOnlyList<EditorTabState> editors, PanelLayoutState? schemaExplorer = null, PanelLayoutState? aiChat = null);
    void Remove(Guid connectionId);
}
