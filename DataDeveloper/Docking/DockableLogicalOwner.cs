using Avalonia;
using Avalonia.Controls;
using Dock.Model.Core;

namespace DataDeveloper.Docking;

/// <summary>
/// Gives Dock's model objects a permanent logical parent. Dock presents a document or tool (a StyledElement) as the
/// content of several controls at once (its tab strip item and the dock's content area); Avalonia makes the first
/// of them its logical parent and clears that parent when that control lets go, while the others still list it as
/// a logical child. Re-attaching such a subtree (switching connection tabs after a query ran in the background)
/// then throws "AttachedToLogicalTreeCore called for 'Document' but control has no logical parent" and the app
/// crashes. With a parent that never changes, those controls neither adopt nor orphan the dockable.
/// </summary>
public static class DockableLogicalOwner
{
    private static readonly StyledElement Owner = new();

    public static void Adopt(IDockable dockable)
    {
        if (dockable is StyledElement { Parent: null } element)
            ((ISetLogicalParent)element).SetParent(Owner);
    }

    /// <summary>Adopts every dockable of a layout declared in XAML.</summary>
    public static void AdoptLayout(IDockable? layout)
    {
        if (layout is null)
            return;

        foreach (var dockable in ConnectionDockFactory.GetDockables(layout))
            Adopt(dockable);
    }

    public static void Release(IDockable dockable)
    {
        if (dockable is StyledElement element && ReferenceEquals(element.Parent, Owner))
            ((ISetLogicalParent)element).SetParent(null);
    }
}
