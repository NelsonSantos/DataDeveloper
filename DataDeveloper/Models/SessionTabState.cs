using System;
using System.Collections.Generic;

namespace DataDeveloper.Models;

public class EditorTabState
{
    public string Name { get; set; } = string.Empty;
    public string? File { get; set; }
    public string SqlStatement { get; set; } = string.Empty;
    public bool IsDirty { get; set; }
}

public class ConnectionSessionState
{
    public Guid ConnectionId { get; set; }
    public List<EditorTabState> Editors { get; set; } = new();
}
