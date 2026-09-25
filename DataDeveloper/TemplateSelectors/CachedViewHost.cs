using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace DataDeveloper.TemplateSelectors;

/// <summary>
/// Lightweight host that lets one cached view be presented by several hosts over time (tab switches, dock
/// documents moved between windows). Templates return a new host on every build; the newest host takes the
/// view from the previous one without touching any presenter. When the host holding the view leaves the
/// visual tree, the view goes back to another host that is still on screen and wants it, so a host that
/// briefly lent its view (e.g. while a floating window was being created) does not stay blank.
/// </summary>
public sealed class CachedViewHost : Decorator
{
    private static readonly ConditionalWeakTable<Control, List<WeakReference<CachedViewHost>>> HostsByView = new();

    private readonly Control _view;

    private CachedViewHost(Control view)
    {
        _view = view;
    }

    public static CachedViewHost Present(Control view)
    {
        var host = new CachedViewHost(view);
        var hosts = HostsByView.GetOrCreateValue(view);
        hosts.RemoveAll(reference => !reference.TryGetTarget(out _));
        hosts.Add(new WeakReference<CachedViewHost>(host));

        // A view shown by a host that is still on screen is taken once this host is attached (see below).
        if (!IsOnScreen(view.Parent))
            host.Claim();

        return host;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (ReferenceEquals(Child, _view))
            return;

        if (_view.Parent is CachedViewHost owner && IsOnScreen(owner) &&
            !ReferenceEquals(TopLevel.GetTopLevel(owner), TopLevel.GetTopLevel(this)))
        {
            // Moving a view out of another window during a layout pass makes Avalonia throw
            // ("InvalidateArrange on wrong LayoutManager"): take it after the pass.
            Dispatcher.UIThread.Post(() =>
            {
                if (this.IsAttachedToVisualTree())
                    Claim();
            }, DispatcherPriority.Background);
            return;
        }

        Claim();
    }

    private static bool IsOnScreen(StyledElement? host) => host is CachedViewHost attached && attached.IsAttachedToVisualTree();

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (!ReferenceEquals(Child, _view))
            return;

        // Hand the view over after the current layout pass: moving it into another window while this window
        // is still laying out makes Avalonia throw ("InvalidateArrange on wrong LayoutManager").
        Dispatcher.UIThread.Post(ReturnViewToAHostOnScreen, DispatcherPriority.Background);
    }

    private void ReturnViewToAHostOnScreen()
    {
        if (_view.Parent is CachedViewHost owner && owner.IsAttachedToVisualTree())
            return;

        if (!HostsByView.TryGetValue(_view, out var hosts))
            return;

        var next = hosts
            .Select(reference => reference.TryGetTarget(out var host) ? host : null)
            .LastOrDefault(host => host is not null && host.IsAttachedToVisualTree());

        next?.Claim();
    }

    private void Claim()
    {
        if (ReferenceEquals(Child, _view))
            return;

        if (_view.Parent is CachedViewHost previous)
        {
            var previousWindow = TopLevel.GetTopLevel(previous);
            previous.Child = null;

            // A view leaving another window may still be queued in that window's layout manager; run that
            // pass now, while the view is detached, or Avalonia throws once the view belongs to this window.
            if (previousWindow is not null && !ReferenceEquals(previousWindow, TopLevel.GetTopLevel(this)))
                previousWindow.UpdateLayout();
        }

        Child = _view;
    }
}
