using System.ComponentModel;
using System.Runtime.CompilerServices;
using DataDeveloper.ViewModels;
using Dock.Model.Avalonia.Controls;
using Dock.Model.Core;

namespace DataDeveloper.Docking;

/// <summary>
/// Creates the dock containers for the app's layouts: connection documents, query editor documents and tools.
/// </summary>
public sealed class ConnectionDockItemContainerGenerator : DockItemContainerGenerator
{
    /// <summary>
    /// Query editors can be reordered in their tab strip (Fill) or floated into their own window (Window);
    /// splitting the document area is intentionally not supported.
    /// </summary>
    public const DockOperationMask QueryEditorOperations = DockOperationMask.Fill | DockOperationMask.Window;

    private readonly ConditionalWeakTable<IDockable, PropertyChangedEventHandler> _editorHandlers = new();

    /// <summary>Dock operations allowed for the documents this generator creates.</summary>
    public DockOperationMask DocumentOperations { get; set; } = QueryEditorOperations;

    public override void PrepareDocumentContainer(IItemsSourceDock dock, IDockable container, object item, int index)
    {
        base.PrepareDocumentContainer(dock, container, item, index);
        DockableLogicalOwner.Adopt(container);

        if (container is IDockableDockingRestrictions restrictions)
            restrictions.AllowedDockOperations = DocumentOperations;

        if (!DocumentOperations.HasFlag(DockOperationMask.Window))
            container.CanFloat = false;

        if (item is not TabQueryEditorViewModel editor)
            return;

        container.IsModified = editor.TextWasChanged;

        PropertyChangedEventHandler handler = (_, e) =>
        {
            if (e.PropertyName == nameof(TabQueryEditorViewModel.TextWasChanged))
                container.IsModified = editor.TextWasChanged;
            else if (e.PropertyName == nameof(TabQueryEditorViewModel.Name))
                container.Title = editor.Name;
        };

        editor.PropertyChanged += handler;
        _editorHandlers.AddOrUpdate(container, handler);
    }

    public override void ClearDocumentContainer(IItemsSourceDock dock, IDockable container, object? item)
    {
        if (item is TabQueryEditorViewModel editor && _editorHandlers.TryGetValue(container, out var handler))
        {
            editor.PropertyChanged -= handler;
            _editorHandlers.Remove(container);
        }

        DockableLogicalOwner.Release(container);
        base.ClearDocumentContainer(dock, container, item);
    }

    public override void PrepareToolContainer(IToolItemsSourceDock dock, IDockable container, object item, int index)
    {
        base.PrepareToolContainer(dock, container, item, index);
        DockableLogicalOwner.Adopt(container);

        // Tools (schema explorer, query results) stay attached to their connection or query: they can be pinned
        // (auto-hide) but not dragged, floated or turned into a tabbed document, so they can never end up closed
        // inside a floating window or mixed with the query tabs.
        container.CanDrag = false;
        container.CanFloat = false;
        container.CanDockAsDocument = false;
    }

    public override void ClearToolContainer(IToolItemsSourceDock dock, IDockable container, object? item)
    {
        DockableLogicalOwner.Release(container);
        base.ClearToolContainer(dock, container, item);
    }
}
