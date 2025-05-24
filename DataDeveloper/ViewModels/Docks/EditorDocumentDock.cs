using Dock.Model.Core;
using Dock.Model.ReactiveUI.Controls;
using ReactiveUI;

namespace DataDeveloper.ViewModels.Docks;

public class EditorDocumentDock : DocumentDock
{
    private static int _countDocument = -1;
    public EditorDocumentDock()
    {
        CreateDocument = ReactiveCommand.Create(CreateNewDocument);
    }

    public static ProportionalDock GetNewEditorDocument(IFactory factory)
    {
        _countDocument++;
        var document = new EditorDocumentViewModel
        {
            Id = $"SqlEditor{_countDocument}",
            Title = $"Sql Statement {_countDocument}",
        };
        var outputTool = new ResultViewModel(document)
        {
            Id = "outputDetails",
            Title = "Results",
            CanClose = false,
            CanFloat = false,
        };

        var messageTool = new MessageViewModel()
        {
            Id = "messageDetails",
            Title = "Messages",
            CanClose = false,
            CanFloat = false,
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
            Proportion = 0.35, 
        };
        var entireDocument = new EditorProportionalDock(document)
        {
            Orientation = Orientation.Vertical,
            Id = document.Id,
            Title = document.Title,
            VisibleDockables = factory.CreateList<IDockable>(document, new ProportionalDockSplitter(), bottom),
        };

        return entireDocument;
        
    }
    
    private void CreateNewDocument()
    {
        var document = GetNewEditorDocument(Factory);
        Factory?.AddDockable(this, document);
        Factory?.SetActiveDockable(document);
        Factory?.SetFocusedDockable(this, document);    
    }    
}

public class EditorProportionalDock : ProportionalDock
{
    private readonly EditorDocumentViewModel _viewModel;

    public EditorProportionalDock(EditorDocumentViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    public override bool OnClose()
    {
        return _viewModel.OnClose();
    }
}