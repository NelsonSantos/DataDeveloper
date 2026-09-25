using System.Collections.Generic;
using System.Linq;
using Dock.Model.Avalonia;
using Dock.Model.Controls;
using Dock.Model.Core;

namespace DataDeveloper.Docking;

/// <summary>
/// Dock factory for a query editor's layout: the editor above its Results tool dock (Message and result tabs).
/// Dock pins tools one at a time; the results are pinned (collapsed to the bottom rail) and restored as a
/// group instead, so collapsing works like a single panel per query.
/// </summary>
public sealed class QueryDockFactory : Factory
{
    /// <summary>Id of the tool dock that holds the query's result tabs.</summary>
    public string? ResultsDockId { get; set; }

    public override void PinDockable(IDockable dockable)
    {
        if (!IsResultsTool(dockable) || FindRoot(dockable, _ => true) is not { } root)
        {
            base.PinDockable(dockable);
            return;
        }

        if (IsDockablePinned(dockable, root))
        {
            ShowResults(root, dockable);
            return;
        }

        if (dockable.Owner is not IToolDock { VisibleDockables: { } visible })
            return;

        foreach (var tool in visible.Where(IsResultsTool).ToList())
            base.PinDockable(tool);
    }

    /// <summary>Restores pinned results into the Results dock, e.g. when a query starts running.</summary>
    public void ShowResults(IRootDock root, IDockable? activate = null)
    {
        var resultsDock = ConnectionDockFactory.GetDockables(root).OfType<IToolDock>().FirstOrDefault(dock => dock.Id == ResultsDockId);
        foreach (var tool in GetPinnedResults(root).ToList())
        {
            // Dock re-initializes pinned tools with the root as owner whenever the layout is re-attached (the
            // query's tab was switched), so point them back at the Results dock before restoring them.
            if (resultsDock is not null)
                tool.OriginalOwner = resultsDock;

            base.PinDockable(tool);
        }

        if (activate is not null && activate.Owner is IDock owner && owner.VisibleDockables?.Contains(activate) == true)
            SetActiveDockable(activate);
    }

    /// <summary>
    /// Drops result tools left on the pinned rail after their tab was closed (Dock only removes closed items
    /// from the dock's visible tools).
    /// </summary>
    public void RemovePinnedResults(IRootDock root, ICollection<object> liveContexts)
    {
        foreach (var tool in GetPinnedResults(root).Where(tool => tool.Context is null || !liveContexts.Contains(tool.Context)).ToList())
            root.BottomPinnedDockables?.Remove(tool);
    }

    private IEnumerable<IDockable> GetPinnedResults(IRootDock root) =>
        root.BottomPinnedDockables?.Where(IsResultsTool) ?? [];

    // Result tools are the only containers generated from items in this layout; their owner is not reliable
    // once pinned (see ShowResults).
    private bool IsResultsTool(IDockable dockable) =>
        ResultsDockId is not null &&
        ((dockable.Owner as IDock)?.Id == ResultsDockId ||
         (dockable.OriginalOwner as IDock)?.Id == ResultsDockId ||
         (dockable.Context is { } item && ReferenceEquals(GetContainerFromItem(item), dockable)));
}
