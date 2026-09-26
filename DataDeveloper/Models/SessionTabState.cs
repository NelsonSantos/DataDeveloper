using System;
using System.Collections.Generic;

namespace DataDeveloper.Models;

public class EditorTabState
{
    public string Name { get; set; } = string.Empty;
    public string? File { get; set; }
    public string SqlStatement { get; set; } = string.Empty;
    public bool IsDirty { get; set; }
    public PanelLayoutState? Results { get; set; }
}

public class ConnectionSessionState
{
    public Guid ConnectionId { get; set; }
    public List<EditorTabState> Editors { get; set; } = new();
    public PanelLayoutState? SchemaExplorer { get; set; }
}

/// <summary>
/// A docked panel's size (its share of the split, between 0 and 1; null keeps the default) and whether it is
/// collapsed to the side rail.
/// </summary>
public sealed record PanelLayoutState(double? Proportion, bool IsCollapsed)
{
    public bool IsDefault => Proportion is null && !IsCollapsed;
}
