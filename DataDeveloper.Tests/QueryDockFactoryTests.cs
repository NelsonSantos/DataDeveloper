using System.Linq;
using Avalonia.Collections;
using Avalonia.Headless.XUnit;
using DataDeveloper.Docking;
using DataDeveloper.Models;
using Dock.Model.Avalonia.Controls;
using Dock.Model.Core;
using Xunit;

namespace DataDeveloper.Tests;

public class QueryDockFactoryTests
{
    [AvaloniaFact]
    public void PinningOneResult_PinsTheWholeResultsDock()
    {
        var (factory, root, results, message, grid) = CreateLayout();

        factory.PinDockable(grid);

        Assert.Empty(results.VisibleDockables!);
        Assert.Equal([message, grid], root.BottomPinnedDockables!.ToList());
    }

    [AvaloniaFact]
    public void UnpinningOneResult_RestoresTheWholeResultsDock()
    {
        var (factory, root, results, message, grid) = CreateLayout();
        factory.PinDockable(grid);

        factory.PinDockable(message);

        Assert.Empty(root.BottomPinnedDockables!);
        Assert.Equal(2, results.VisibleDockables!.Count);
        Assert.Contains(message, results.VisibleDockables);
        Assert.Contains(grid, results.VisibleDockables);
    }

    [AvaloniaFact]
    public void ShowResults_RestoresPinnedResults()
    {
        var (factory, root, results, message, grid) = CreateLayout();
        factory.PinDockable(message);

        factory.ShowResults(root);

        Assert.Empty(root.BottomPinnedDockables!);
        Assert.Equal(2, results.VisibleDockables!.Count);
    }

    [AvaloniaFact]
    public void ShowResults_RestoresResultsPinnedBeforeTheLayoutWasReinitialized()
    {
        var (factory, root, results, _, grid) = CreateLayout();
        factory.PinDockable(grid);

        // Switching query tabs re-attaches the layout, and Dock re-owns pinned tools by the root.
        factory.InitLayout(root);
        factory.ShowResults(root);

        Assert.Empty(root.BottomPinnedDockables!);
        Assert.Equal(2, results.VisibleDockables!.Count);
    }

    [AvaloniaFact]
    public void RemovePinnedResults_DropsPinnedResultsOfClosedTabs()
    {
        var (factory, root, _, message, grid) = CreateLayout();
        factory.PinDockable(message);

        factory.RemovePinnedResults(root, [message.Context!]);

        Assert.Equal([message], root.BottomPinnedDockables!.ToList());
    }

    [AvaloniaFact]
    public void RestoringCollapsedResults_PinsTheWholeResultsDock()
    {
        var (factory, root, results, message, grid) = CreateLayout();
        var editor = root.VisibleDockables!.OfType<ProportionalDock>().Single().VisibleDockables!.OfType<Document>().Single();

        using var binding = ToolDockLayoutBinding.Attach(factory, root, results, editor, new PanelLayoutState(null, true), _ => { });

        Assert.Empty(results.VisibleDockables!);
        Assert.Equal([message, grid], root.BottomPinnedDockables!.ToList());
    }

    private static (QueryDockFactory Factory, RootDock Root, ToolDock Results, Tool Message, Tool Grid) CreateLayout()
    {
        var factory = new QueryDockFactory { ResultsDockId = "QueryResults" };
        var results = new ToolDock { Id = "QueryResults", Alignment = Alignment.Bottom, VisibleDockables = new AvaloniaList<IDockable>(), ToolTemplate = new ToolTemplate() };
        var editor = new Document { Id = "EditorPane" };
        var layout = new ProportionalDock { Id = "QueryLayout", VisibleDockables = new AvaloniaList<IDockable> { editor, results } };
        var root = new RootDock { Id = "QueryRoot", VisibleDockables = new AvaloniaList<IDockable> { layout }, ActiveDockable = layout };

        factory.InitLayout(root);

        // Like the view, the result tabs arrive through ItemsSource once the layout is initialized.
        results.ItemsSource = new AvaloniaList<object> { "message", "result" };
        var message = (Tool)results.VisibleDockables!.Single(tool => Equals(tool.Context, "message"));
        var grid = (Tool)results.VisibleDockables!.Single(tool => Equals(tool.Context, "result"));
        return (factory, root, results, message, grid);
    }
}
