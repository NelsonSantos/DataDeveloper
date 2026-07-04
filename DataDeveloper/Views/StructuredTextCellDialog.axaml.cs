using Avalonia.Controls;
using DataDeveloper.Interfaces;

namespace DataDeveloper.Views;

public partial class StructuredTextCellDialog : Window
{
    private const string WindowSizeKey = "structured-text-cell-dialog";
    private readonly IWindowStateService _windowStateService;

    public StructuredTextCellDialog(IWindowStateService windowStateService)
    {
        InitializeComponent();
        _windowStateService = windowStateService;
        _windowStateService.RestoreSize(WindowSizeKey, this);
        Closing += OnClosing;
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        _windowStateService.SaveSize(WindowSizeKey, this);
    }
}
