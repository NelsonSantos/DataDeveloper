using Avalonia.Headless.XUnit;
using DataDeveloper.Docking;
using Dock.Model.Avalonia.Controls;
using Dock.Model.Core;
using Xunit;

namespace DataDeveloper.Tests;

public class ConnectionDockItemContainerGeneratorTests
{
    [AvaloniaFact]
    public void ToolContainers_StayInTheirToolDock()
    {
        var generator = new ConnectionDockItemContainerGenerator();
        var tool = new Tool();

        generator.PrepareToolContainer(new ToolDock(), tool, "Schema Explorer", 0);

        Assert.False(tool.CanDrag);
        Assert.False(tool.CanFloat);
        Assert.False(tool.CanDockAsDocument);
    }

    [AvaloniaFact]
    public void DocumentOperations_WithoutWindow_KeepDocumentsFromFloating()
    {
        var generator = new ConnectionDockItemContainerGenerator { DocumentOperations = DockOperationMask.Fill };
        var document = new Document();

        generator.PrepareDocumentContainer(new DocumentDock(), document, "connection", 0);

        Assert.False(document.CanFloat);
        Assert.Equal(DockOperationMask.Fill, document.AllowedDockOperations);
    }
}
