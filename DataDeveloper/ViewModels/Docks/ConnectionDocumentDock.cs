using DataDeveloper.Data.Interfaces;
using DataDeveloper.Interfaces;
using Dock.Model.Core;
using Dock.Model.ReactiveUI.Controls;

namespace DataDeveloper.ViewModels.Docks;

public class ConnectionDocumentDock : DocumentDock
{
    private readonly IAuxFactory _factory;

    public ConnectionDocumentDock(IAuxFactory factory, IConnectionSettings connectionSettings)
    {
        _factory = factory;
        AddNewConnection(connectionSettings);
    }

    public void AddNewConnection(IConnectionSettings connectionSettings)
    {
        var treeTool = new ConnectionDetailsViewModel(connectionSettings)
        {
            Id = "connectionDetails",
            Title = "Tables",
            CanClose = false,
            CanFloat = false,
            Proportion = 0.25,
        };
        
        var left = new ProportionalDock()
        {
            Proportion = 0.25,
            Orientation = Orientation.Vertical,
            Id = "Left",
            Title = "Left",
            ActiveDockable = null,
            VisibleDockables = _factory.CreateList<IDockable>
            ( 
                new ToolDock
                {
                    ActiveDockable = treeTool,
                    VisibleDockables = _factory.CreateList<IDockable>(treeTool),
                    Alignment = Alignment.Left
                }
            ),
        };
        
        var documentDock = new EditorDocumentDock(connectionSettings)
        {
            IsCollapsable = false,
            Id = "Documents",
            Title = "Documents",
            CanCreateDocument = true,
            Proportion = 0.75,
        };
        var entireDocument = documentDock.GetNewEditorDocument(_factory/*, connectionSettings*/);
        documentDock.ActiveDockable = entireDocument;
        documentDock.VisibleDockables = _factory.CreateList<IDockable>(entireDocument);
        
        var mainLayout = new ProportionalDock
        {
            Title = $"{connectionSettings.Name} ({connectionSettings.DatabaseType})",
            Orientation = Orientation.Horizontal,
            VisibleDockables = _factory.CreateList<IDockable>( left, new ProportionalDockSplitter(), documentDock ),
        };

        _factory.AddDockable(this, mainLayout);
        _factory.SetActiveDockable(mainLayout);
        _factory.SetFocusedDockable(this, mainLayout);    
        
    }
}