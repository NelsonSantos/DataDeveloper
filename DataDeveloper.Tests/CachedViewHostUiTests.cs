using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DataDeveloper.TemplateSelectors;
using Xunit;

namespace DataDeveloper.Tests;

public class CachedViewHostUiTests
{
    [AvaloniaFact]
    public void TabControlMovedToAnotherWindowAndBack_KeepsShowingTheCachedView()
    {
        // Mirrors a query editor (with a result TabControl) floated into a dock window and docked back.
        var view = new TextBlock { Text = "result grid" };
        var tabs = new TabControl
        {
            ItemsSource = new[] { "message", "result" },
            ContentTemplate = new CachingTemplate(view),
            SelectedIndex = 1
        };
        var editor = new Border { Child = tabs };
        var mainHost = new ContentControl();
        var floatingHost = new ContentControl();
        var mainWindow = new Window { Content = mainHost };
        var floatingWindow = new Window { Content = floatingHost };
        mainWindow.Show();
        floatingWindow.Show();

        mainHost.Content = editor;
        Dispatcher.UIThread.RunJobs();
        Assert.True(view.IsAttachedToVisualTree());

        Move(mainHost, floatingHost);
        Assert.Same(floatingWindow, TopLevel.GetTopLevel(view));

        Move(floatingHost, mainHost);
        Assert.Same(mainWindow, TopLevel.GetTopLevel(view));

        mainWindow.Close();
        floatingWindow.Close();

        // Dock detaches and re-hosts a document in separate layout passes. (Moving a subtree between two
        // windows within one pass makes Avalonia throw "InvalidateArrange on wrong LayoutManager", cache or not.)
        void Move(ContentControl from, ContentControl to)
        {
            from.Content = null;
            Dispatcher.UIThread.RunJobs();
            to.Content = editor;
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void CachedViewRequestedByASecondHost_MovesToTheNewHost()
    {
        var view = new TextBlock { Text = "editor view" };
        var template = new CachingTemplate(view);
        var first = new ContentControl { ContentTemplate = template };
        var second = new ContentControl { ContentTemplate = template };
        var window = new Window { Content = new StackPanel { Children = { first, second } } };
        window.Show();

        first.Content = "result";
        Dispatcher.UIThread.RunJobs();
        Assert.True(first.IsVisualAncestorOf(view));

        second.Content = "result";
        Dispatcher.UIThread.RunJobs();

        Assert.True(second.IsVisualAncestorOf(view));
        Assert.False(first.IsVisualAncestorOf(view));
        window.Close();
    }

    [AvaloniaFact]
    public void HostThatLentItsView_GetsItBackWhenTheBorrowerLeavesTheScreen()
    {
        // While a dock document is being floated, the new window can briefly present the main area's
        // document; when it moves on, the main area must show that document again instead of staying blank.
        var view = new TextBlock { Text = "Query 1" };
        var template = new CachingTemplate(view);
        var mainArea = new ContentControl { ContentTemplate = template, Content = "result" };
        var floatingArea = new ContentControl { ContentTemplate = template };
        var mainWindow = new Window { Content = mainArea };
        var floatingWindow = new Window { Content = floatingArea };
        mainWindow.Show();
        floatingWindow.Show();
        Dispatcher.UIThread.RunJobs();

        floatingArea.Content = "result";
        Dispatcher.UIThread.RunJobs();
        Assert.Same(floatingWindow, TopLevel.GetTopLevel(view));

        floatingArea.Content = "message";
        Dispatcher.UIThread.RunJobs();

        Assert.Same(mainWindow, TopLevel.GetTopLevel(view));
        mainWindow.Close();
        floatingWindow.Close();
    }

    [AvaloniaFact]
    public void Present_DetachesTheViewFromItsPreviousHost()
    {
        var view = new TextBlock();
        var previous = CachedViewHost.Present(view);

        var next = CachedViewHost.Present(view);

        Assert.Null(previous.Child);
        Assert.Same(view, next.Child);
    }

    private sealed class CachingTemplate(Control view) : IDataTemplate
    {
        public Control? Build(object? param) =>
            param as string == "result" ? CachedViewHost.Present(view) : new TextBlock { Text = "message" };

        public bool Match(object? data) => data is string;
    }
}
