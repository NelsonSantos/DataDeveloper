using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;

namespace DataDeveloper.Docking;

/// <summary>
/// Floating window for query documents. Dock turns a floating window into a borderless "tool window" (no title bar
/// or window buttons, dragged by the tool's header, closed by the tool's close button) as soon as any tool chrome
/// appears in it. Each query hosts its own Results tool dock, so that must not happen while the window holds
/// documents: the window keeps its native decorations and the results keep their normal (pinnable) chrome.
/// </summary>
public sealed class DocumentHostWindow : HostWindow
{
    static DocumentHostWindow()
    {
        // Refuse the switch instead of undoing it: IsToolWindow swaps the whole window template, which re-creates
        // the Results chrome, which switches it again (the window flickers with an empty background).
        IsToolWindowProperty.OverrideMetadata<DocumentHostWindow>(new StyledPropertyMetadata<bool>(coerce: CoerceIsToolWindow));
    }

    protected override Type StyleKeyOverride => typeof(HostWindow);

    public DocumentHostWindow()
    {
        ToolChromeControlsWholeWindow = false;
    }

    private static bool CoerceIsToolWindow(AvaloniaObject sender, bool value)
    {
        if (!value || sender is not DocumentHostWindow window || !window.HostsDocuments())
            return value;

        // A tool chrome just attached itself to the window as its grip; hand it back its normal behavior.
        Dispatcher.UIThread.Post(window.ReleaseToolChromes, DispatcherPriority.Background);
        return false;
    }

    private bool HostsDocuments() =>
        DataContext is IRootDock root && ConnectionDockFactory.GetDockables(root).Any(dockable => dockable is IDocument and not ITool);

    private void ReleaseToolChromes()
    {
        foreach (var chrome in this.GetVisualDescendants().OfType<ToolChromeControl>())
        {
            DetachGrip(chrome);
            chrome.SetCurrentValue(ToolChromeControl.IsFloatingProperty, false);
            ((IPseudoClasses)chrome.Classes).Remove(":floating");
        }
    }
}
