using System.Threading.Tasks;
using Avalonia.Controls;
using DataDeveloper.Interfaces;
using DataDeveloper.NextGrid.Renderers;
using DataDeveloper.ViewModels;
using DataDeveloper.Views;

namespace DataDeveloper.Services;

public sealed class StructuredTextCellDialogService : IStructuredTextCellDialogService
{
    private readonly IWindowStateService _windowStateService;

    public StructuredTextCellDialogService(IWindowStateService windowStateService)
    {
        _windowStateService = windowStateService;
    }

    public async Task<string?> ShowDialogAsync(Window parentWindow, string? value, bool isEditable, StructuredTextKind kind)
    {
        var model = new StructuredTextCellDialogViewModel();
        model.Initialize(value, isEditable, kind);

        var dialog = new StructuredTextCellDialog(_windowStateService) { DataContext = model };

        var confirmed = await dialog.ShowDialog<bool?>(parentWindow);
        return confirmed == true ? model.CurrentText : null;
    }
}
