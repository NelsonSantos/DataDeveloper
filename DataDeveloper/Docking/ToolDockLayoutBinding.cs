using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using DataDeveloper.Models;
using Dock.Model.Avalonia.Core;
using Dock.Model.Controls;
using Dock.Model.Core;

namespace DataDeveloper.Docking;

/// <summary>
/// Keeps a tool dock's size (its proportion of the split it shares with one sibling) and collapsed state (its tools
/// pinned to the root's side rail) in step with a saved <see cref="PanelLayoutState"/>: applies the saved state once,
/// then reports every change the user makes.
/// </summary>
public sealed class ToolDockLayoutBinding : IDisposable
{
    private readonly IFactory _factory;
    private readonly IRootDock _root;
    private readonly DockableBase _toolDock;
    private readonly Action<PanelLayoutState> _changed;

    private ToolDockLayoutBinding(IFactory factory, IRootDock root, DockableBase toolDock, Action<PanelLayoutState> changed)
    {
        _factory = factory;
        _root = root;
        _toolDock = toolDock;
        _changed = changed;
    }

    public static ToolDockLayoutBinding Attach(
        IFactory factory,
        IRootDock root,
        IToolDock toolDock,
        IDockable sibling,
        PanelLayoutState? saved,
        Action<PanelLayoutState> changed)
    {
        if (toolDock is not DockableBase toolDockBase)
            throw new ArgumentException("The tool dock must be an Avalonia dock model.", nameof(toolDock));

        if (saved?.Proportion is { } proportion && IsValidProportion(proportion))
        {
            toolDock.Proportion = proportion;
            sibling.Proportion = 1 - proportion;
        }

        // A single pin collapses the whole dock: Dock pins its only tool, QueryDockFactory pins the results as a group.
        if (saved?.IsCollapsed == true && toolDock.VisibleDockables?.FirstOrDefault() is { } tool)
            factory.PinDockable(tool);

        var binding = new ToolDockLayoutBinding(factory, root, toolDockBase, changed);
        toolDockBase.PropertyChanged += binding.OnToolDockPropertyChanged;
        factory.DockablePinned += binding.OnPinnedChanged;
        factory.DockableUnpinned += binding.OnPinnedChanged;
        return binding;
    }

    public PanelLayoutState Current => new(
        IsValidProportion(_toolDock.Proportion) ? _toolDock.Proportion : null,
        GetPinnedDockables()?.Count > 0);

    public void Dispose()
    {
        _toolDock.PropertyChanged -= OnToolDockPropertyChanged;
        _factory.DockablePinned -= OnPinnedChanged;
        _factory.DockableUnpinned -= OnPinnedChanged;
    }

    private void OnToolDockPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        // Proportions outside (0, 1) are transient layout values, not a size the user chose.
        if (e.Property == DockableBase.ProportionProperty && IsValidProportion(_toolDock.Proportion))
            _changed(Current);
    }

    private void OnPinnedChanged(object? sender, EventArgs e) => _changed(Current);

    // Each layout has a single tool dock, so its side of the root's rail holds only that dock's pinned tools.
    private IList<IDockable>? GetPinnedDockables() => ((IToolDock)_toolDock).Alignment switch
    {
        Alignment.Right => _root.RightPinnedDockables,
        Alignment.Top => _root.TopPinnedDockables,
        Alignment.Bottom => _root.BottomPinnedDockables,
        _ => _root.LeftPinnedDockables
    };

    private static bool IsValidProportion(double proportion) => proportion is > 0 and < 1;
}
