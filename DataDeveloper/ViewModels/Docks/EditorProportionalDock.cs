using Dock.Model.ReactiveUI.Controls;

namespace DataDeveloper.ViewModels.Docks;

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