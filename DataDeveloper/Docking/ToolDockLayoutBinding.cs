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
/// then reports every change the user makes. A null sibling leaves the neighbour's size to the caller.
/// </summary>
public sealed class ToolDockLayoutBinding : IDisposable
{
    private readonly IFactory _factory;
    private readonly IRootDock _root;
    private readonly DockableBase _toolDock;
    private readonly Action<PanelLayoutState> _changed;
    private readonly PinnedWidthOptions? _pinnedWidth;
    private readonly List<DockableBase> _tools;

    private ToolDockLayoutBinding(
        IFactory factory, IRootDock root, DockableBase toolDock, Action<PanelLayoutState> changed, PinnedWidthOptions? pinnedWidth,
        List<DockableBase> tools)
    {
        _factory = factory;
        _root = root;
        _toolDock = toolDock;
        _changed = changed;
        _pinnedWidth = pinnedWidth;
        _tools = tools;
    }

    /// <summary>
    /// Makes the overlay a collapsed dock opens as remember its width. A tool pinned before it was ever laid out has no
    /// stored size and Dock then opens its overlay a few dozen pixels wide, so an unset or too narrow one gets
    /// <see cref="Default"/>; a width the user dragged it to is kept. Dock raises no event while the overlay is resized, so the width is read from
    /// <see cref="Current"/> whenever the layout is saved.
    /// </summary>
    public sealed record PinnedWidthOptions(double Default, double Minimum, Func<double> Height);

    public static ToolDockLayoutBinding Attach(
        IFactory factory,
        IRootDock root,
        IToolDock toolDock,
        IDockable? sibling,
        PanelLayoutState? saved,
        Action<PanelLayoutState> changed,
        PinnedWidthOptions? pinnedWidth = null)
    {
        if (toolDock is not DockableBase toolDockBase)
            throw new ArgumentException("The tool dock must be an Avalonia dock model.", nameof(toolDock));

        // Taken before the pin below, which moves the tools out of the dock's visible list.
        var tools = toolDock.VisibleDockables?.OfType<DockableBase>().ToList() ?? [];

        if (saved?.Proportion is { } proportion && IsValidProportion(proportion))
        {
            toolDock.Proportion = proportion;
            if (sibling is not null)
                sibling.Proportion = 1 - proportion;
        }

        // A single pin collapses the whole dock: Dock pins its only tool, QueryDockFactory pins the results as a group.
        if (saved?.IsCollapsed == true && toolDock.VisibleDockables?.FirstOrDefault() is { } tool)
            factory.PinDockable(tool);

        var binding = new ToolDockLayoutBinding(factory, root, toolDockBase, changed, pinnedWidth, tools);
        binding.ApplyPinnedWidth(saved?.PinnedWidth);
        toolDockBase.PropertyChanged += binding.OnToolDockPropertyChanged;
        factory.DockablePinned += binding.OnPinnedChanged;
        factory.DockableUnpinned += binding.OnPinnedChanged;
        return binding;
    }

    public PanelLayoutState Current => new(
        IsValidProportion(_toolDock.Proportion) ? _toolDock.Proportion : null,
        GetPinnedDockables()?.Count > 0,
        CurrentPinnedWidth());

    public void Dispose()
    {
        _toolDock.PropertyChanged -= OnToolDockPropertyChanged;
        _factory.DockablePinned -= OnPinnedChanged;
        _factory.DockableUnpinned -= OnPinnedChanged;
    }

    // Only a width the user chose is saved: the default is what an unset one falls back to anyway.
    private double? CurrentPinnedWidth()
    {
        if (_pinnedWidth is null || _tools.Count == 0)
            return null;

        _tools[0].GetPinnedBounds(out _, out _, out var width, out _);
        return width >= _pinnedWidth.Minimum && Math.Abs(width - _pinnedWidth.Default) > 0.5 ? width : null;
    }

    // An unset size is NaN, and every comparison with NaN is false, so test for "usable" rather than "too small".
    private void ApplyPinnedWidth(double? saved)
    {
        if (_pinnedWidth is null)
            return;

        var height = Math.Max(_pinnedWidth.Height(), 400);
        foreach (var tool in _tools)
        {
            tool.GetPinnedBounds(out _, out _, out var width, out var currentHeight);
            var usable = width >= _pinnedWidth.Minimum && currentHeight > 0;
            if (saved is { } savedWidth && savedWidth >= _pinnedWidth.Minimum)
                tool.SetPinnedBounds(0, 0, savedWidth, height);
            else if (!usable)
                tool.SetPinnedBounds(0, 0, _pinnedWidth.Default, height);
        }
    }

    private void OnToolDockPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        // Proportions outside (0, 1) are transient layout values, not a size the user chose.
        if (e.Property == DockableBase.ProportionProperty && IsValidProportion(_toolDock.Proportion))
            _changed(Current);
    }

    private void OnPinnedChanged(object? sender, EventArgs e)
    {
        // Pinning may hand the tool an unset size; the width that was in effect is kept, else the default is given.
        ApplyPinnedWidth(null);
        _changed(Current);
    }

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
