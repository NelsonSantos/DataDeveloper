using System.Collections.Generic;
using System.Linq;
using Dock.Model.Avalonia;
using Dock.Model.Controls;
using Dock.Model.Core;

namespace DataDeveloper.Docking;

/// <summary>
/// Dock factory for a connection's layout. Closing a floating window (its native close button) returns its
/// documents to the main document dock instead of closing them, so no query editor is lost and no save prompt
/// opens away from the window the user was looking at. Closing a single tab still asks to save.
/// </summary>
public sealed class ConnectionDockFactory : Factory
{
    /// <summary>Id of the document dock in the main layout that receives documents from closed windows.</summary>
    public string? MainDocumentDockId { get; set; }

    public override void CloseWindow(IDockWindow window)
    {
        // Dock closes a window's dockables here, before raising WindowClosing, so documents must move out first.
        ReturnDocumentsToMainDock(window);
        base.CloseWindow(window);
    }

    private void ReturnDocumentsToMainDock(IDockWindow window)
    {
        if (MainDocumentDockId is null ||
            window.Layout is not { } windowLayout ||
            window.Owner is not IRootDock mainRoot ||
            GetDockables(mainRoot).OfType<IDocumentDock>().FirstOrDefault(dock => dock.Id == MainDocumentDockId) is not { } mainDocuments)
            return;

        IDocument? lastMoved = null;
        foreach (var document in GetDockables(windowLayout).OfType<IDocument>().ToList())
        {
            if (document.Owner is not IDock owner)
                continue;

            MoveDockable(owner, mainDocuments, document, null);
            lastMoved = document;
        }

        if (lastMoved is not null)
            SetActiveDockable(lastMoved);
    }

    internal static IEnumerable<IDockable> GetDockables(IDockable dockable)
    {
        yield return dockable;

        if (dockable is not IDock { VisibleDockables: { } children })
            yield break;

        foreach (var child in children.ToList())
        {
            foreach (var descendant in GetDockables(child))
                yield return descendant;
        }
    }
}
