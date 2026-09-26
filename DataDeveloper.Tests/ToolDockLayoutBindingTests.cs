using System.Collections.Generic;
using Avalonia.Collections;
using Avalonia.Headless.XUnit;
using DataDeveloper.Docking;
using DataDeveloper.Models;
using Dock.Model.Avalonia;
using Dock.Model.Avalonia.Controls;
using Dock.Model.Core;
using Xunit;

namespace DataDeveloper.Tests;

public class ToolDockLayoutBindingTests
{
    [AvaloniaFact]
    public void Attach_AppliesTheSavedProportionToTheDockAndItsSibling()
    {
        var layout = CreateLayout();

        using var binding = layout.Attach(new PanelLayoutState(0.35, false));

        Assert.Equal(0.35, layout.Tools.Proportion);
        Assert.Equal(0.65, layout.Documents.Proportion, 6);
        Assert.Empty(layout.Changes);
    }

    [AvaloniaFact]
    public void Attach_CollapsesTheDock_WhenSavedCollapsed()
    {
        var layout = CreateLayout();

        using var binding = layout.Attach(new PanelLayoutState(null, true));

        Assert.Empty(layout.Tools.VisibleDockables!);
        Assert.Equal([layout.Tool], layout.Root.LeftPinnedDockables!);
        Assert.True(binding.Current.IsCollapsed);
    }

    [AvaloniaFact]
    public void Attach_IgnoresAnInvalidSavedProportion()
    {
        var layout = CreateLayout();

        using var binding = layout.Attach(new PanelLayoutState(1.5, false));

        Assert.Equal(0.25, layout.Tools.Proportion);
    }

    [AvaloniaFact]
    public void ResizingTheDock_ReportsTheNewProportion()
    {
        var layout = CreateLayout();
        using var binding = layout.Attach(null);

        layout.Tools.Proportion = 0.4;

        Assert.Equal(new PanelLayoutState(0.4, false), Assert.Single(layout.Changes));
    }

    [AvaloniaFact]
    public void CollapsingAndExpandingTheDock_ReportsTheCollapsedState()
    {
        var layout = CreateLayout();
        using var binding = layout.Attach(null);

        layout.Factory.PinDockable(layout.Tool);
        layout.Factory.PinDockable(layout.Tool);

        Assert.Equal([new PanelLayoutState(0.25, true), new PanelLayoutState(0.25, false)], layout.Changes);
    }

    [AvaloniaFact]
    public void Dispose_StopsReporting()
    {
        var layout = CreateLayout();
        var binding = layout.Attach(null);

        binding.Dispose();
        layout.Tools.Proportion = 0.4;
        layout.Factory.PinDockable(layout.Tool);

        Assert.Empty(layout.Changes);
    }

    private static TestLayout CreateLayout()
    {
        var factory = new Factory();
        var tool = new Tool { Id = "SchemaExplorer" };
        var tools = new ToolDock { Id = "SchemaTools", Alignment = Alignment.Left, Proportion = 0.25, VisibleDockables = new AvaloniaList<IDockable> { tool } };
        var documents = new DocumentDock { Id = "QueryDocuments", VisibleDockables = new AvaloniaList<IDockable>() };
        var main = new ProportionalDock { Id = "MainLayout", VisibleDockables = new AvaloniaList<IDockable> { tools, new ProportionalDockSplitter(), documents } };
        var root = new RootDock { Id = "Root", VisibleDockables = new AvaloniaList<IDockable> { main }, ActiveDockable = main };
        factory.InitLayout(root);
        return new TestLayout(factory, root, tools, documents, tool);
    }

    private sealed record TestLayout(Factory Factory, RootDock Root, ToolDock Tools, DocumentDock Documents, Tool Tool)
    {
        public List<PanelLayoutState> Changes { get; } = new();

        public ToolDockLayoutBinding Attach(PanelLayoutState? saved) =>
            ToolDockLayoutBinding.Attach(Factory, Root, Tools, Documents, saved, Changes.Add);
    }
}
