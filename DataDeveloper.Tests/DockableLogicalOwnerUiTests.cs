using System;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using DataDeveloper.Docking;
using Dock.Model.Avalonia.Controls;
using Xunit;

namespace DataDeveloper.Tests;

public class DockableLogicalOwnerUiTests
{
    [AvaloniaFact]
    public void DockablePresentedByTwoControls_SurvivesTheFirstLettingGo()
    {
        // Dock shows a document in its tab strip item and in the content area at once. When the control that
        // became its logical parent lets go, the other one must still re-attach cleanly (connection tab switch).
        var document = new Document { Id = "query", Title = "Query 1" };
        DockableLogicalOwner.Adopt(document);

        Assert.Null(RepresentWithTwoControls(document));
    }

    [AvaloniaFact]
    public void DockableWithoutOwner_CrashesWhenReattached()
    {
        // Documents what DockableLogicalOwner prevents.
        var exception = RepresentWithTwoControls(new Document { Id = "query", Title = "Query 1" });

        Assert.Contains("no logical parent", Assert.IsType<InvalidOperationException>(exception).Message);
    }

    private static Exception? RepresentWithTwoControls(Document document)
    {
        var tabItem = new ContentControl();
        var contentArea = new ContentControl();
        var host = new ContentControl { Content = new StackPanel { Children = { tabItem, contentArea } } };
        var window = new Window { Content = host };
        window.Show();

        tabItem.Content = document;
        contentArea.Content = document;
        Dispatcher.UIThread.RunJobs();

        var subtree = host.Content;
        host.Content = null;
        tabItem.Content = null;
        Dispatcher.UIThread.RunJobs();

        var exception = Record.Exception(() =>
        {
            host.Content = subtree;
            Dispatcher.UIThread.RunJobs();
        });

        window.Close();
        return exception;
    }
}
