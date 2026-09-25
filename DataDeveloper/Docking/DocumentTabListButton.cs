using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;

namespace DataDeveloper.Docking;

/// <summary>
/// Button at the right end of a document tab strip that lists all its documents, so tabs scrolled out of view can
/// be reached directly. Dock only scrolls an overflowing tab strip; the button shows up only while it overflows.
/// Its DataContext is the tab strip's document dock.
/// </summary>
public sealed class DocumentTabListButton : Button
{
    private ScrollViewer? _scrollViewer;

    protected override Type StyleKeyOverride => typeof(Button);

    public DocumentTabListButton()
    {
        Content = new TextBlock { Text = "\U000F0140", Classes = { "MaterialIcon" }, FontSize = 16 };
        Background = Avalonia.Media.Brushes.Transparent;
        Padding = new Thickness(4, 0);
        VerticalAlignment = VerticalAlignment.Center;
        IsVisible = false;
        ToolTip.SetTip(this, "Show all tabs");
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _scrollViewer = this.FindAncestorOfType<DocumentTabStrip>()?
            .GetVisualDescendants()
            .OfType<ScrollViewer>()
            .FirstOrDefault(scrollViewer => scrollViewer.Name == "PART_ScrollViewer");

        if (_scrollViewer is null)
            return;

        _scrollViewer.PropertyChanged += OnScrollViewerPropertyChanged;
        UpdateVisibility();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (_scrollViewer is not null)
            _scrollViewer.PropertyChanged -= OnScrollViewerPropertyChanged;

        _scrollViewer = null;
    }

    private void OnScrollViewerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ScrollViewer.ExtentProperty || e.Property == ScrollViewer.ViewportProperty)
            UpdateVisibility();
    }

    private void UpdateVisibility() =>
        IsVisible = _scrollViewer is { } scrollViewer && scrollViewer.Extent.Width > scrollViewer.Viewport.Width + 1;

    protected override void OnClick()
    {
        base.OnClick();

        if (DataContext is not IDock { Factory: { } factory, VisibleDockables: { } dockables } dock)
            return;

        var menu = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedRight };
        foreach (var document in dockables.OfType<IDocument>())
        {
            var item = new MenuItem
            {
                Header = document.Title,
                FontWeight = ReferenceEquals(document, dock.ActiveDockable) ? Avalonia.Media.FontWeight.Bold : Avalonia.Media.FontWeight.Normal
            };
            item.Click += (_, _) =>
            {
                factory.SetActiveDockable(document);
                factory.SetFocusedDockable(dock, document);
            };
            menu.Items.Add(item);
        }

        menu.ShowAt(this);
    }
}
