using DataDeveloper.Data.Interfaces;
using Dock.Model.Core;
using Dock.Model.ReactiveUI.Controls;
using ReactiveUI;

namespace DataDeveloper.ViewModels.Docks;

public class EditorDocumentDock : DocumentDock
{
    private IConnectionSettings _connectionSettings;
    private static int _countDocument = -1;
    public EditorDocumentDock(IConnectionSettings connectionSettings)
    {
        _connectionSettings = connectionSettings;
        CreateDocument = ReactiveCommand.Create(CreateNewDocument);
    }

    public ProportionalDock GetNewEditorDocument(IFactory factory/*, IConnectionSettings connectionSettings*/)
    {
        _countDocument++;
        var document = new EditorDocumentViewModel(_connectionSettings)
        {
            Id = $"SqlEditor{_countDocument}",
            Title = $"Sql Statement {_countDocument}",
            Proportion = 0.5
        };
        var outputTool = new ResultViewModel(factory, document)
        {
            Id = "outputDetails",
            Title = "Results",
            CanClose = false,
            CanFloat = false,
            Proportion = 0.5
        };

        var messageTool = new MessageViewModel(factory, document)
        {
            Id = "messageDetails",
            Title = "Messages",
            CanClose = false,
            CanFloat = false,
            Proportion = 0.5
        };

        var bottom = new ToolDock
        {
            Id = "Bottom",
            Title = "Bottom",
            ActiveDockable = outputTool,
            VisibleDockables = factory.CreateList<IDockable>( outputTool, messageTool ),
            Alignment = Alignment.Bottom,
            CanClose = false,
            CanFloat = false,
            Proportion = 0.5, 
        };
        var entireDocument = new EditorProportionalDock(document)
        {
            Orientation = Orientation.Vertical,
            Id = document.Id,
            Title = document.Title,
            VisibleDockables = factory.CreateList<IDockable>(document, new ProportionalDockSplitter(), bottom),
            Proportion = 0.75,
        };

        return entireDocument;
        
    }
    
    private void CreateNewDocument()
    {
        var document = GetNewEditorDocument(Factory/*, _connectionSettings*/);
        Factory?.AddDockable(this, document);
        Factory?.SetActiveDockable(document);
        Factory?.SetFocusedDockable(this, document);    
    }    
}