using System;
using System.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.VisualTree;
using DataDeveloper.Interfaces;
using DataDeveloper.Services;
using DataDeveloper.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DataDeveloper.Views;

public partial class MainWindow : Window, IMainWindow
{
    //private readonly IServiceScopeFactory _scopeFactory;
    //private IServiceScope? _currentScope;
    private readonly MainWindowViewModel _viewModel;
    private readonly IWindowStateService _windowStateService;
    private Guid Id { get; } = Guid.NewGuid();
    public MainWindow(IWindowStateService windowStateService, MainWindowViewModel viewModel)
    {
        InitializeComponent();
        _windowStateService = windowStateService;
        _windowStateService?.Restore(this);
        _viewModel = viewModel;
        DataContext = _viewModel;
        SetAppIcon();
        
        this.Closing += OnClosing;

        // macOS consumes Control-Tab before it reaches the app; there the Window menu items handle it.
        if (!OperatingSystem.IsMacOS())
            AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    // Runs before the SQL editor, which would treat Ctrl+Tab as indent.
    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Tab || !e.KeyModifiers.HasFlag(KeyModifiers.Control) || GetVisibleConnectionView() is not { } view)
            return;

        view.ShowAdjacentQuery(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -1 : 1);
        e.Handled = true;
    }

    private void OnShowNextQueryTab(object? sender, EventArgs e) => GetVisibleConnectionView()?.ShowAdjacentQuery(1);

    private void OnShowPreviousQueryTab(object? sender, EventArgs e) => GetVisibleConnectionView()?.ShowAdjacentQuery(-1);

    private TabConnectionView? GetVisibleConnectionView() =>
        this.GetVisualDescendants().OfType<TabConnectionView>().FirstOrDefault(view => view.IsEffectivelyVisible);

    private void SetAppIcon()
    {
        string platform = OperatingSystem.IsWindows() ? "ico" : "png";
        string path = $"avares://{GetType().Assembly.GetName().Name}/Assets/Icons/AppIcon.{platform}";

        var icon = new WindowIcon(AssetLoader.Open(new Uri(path)));
        this.Icon = icon;
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        e.Cancel = true;
        _ = HandleClosingAsync();
    }
    private async Task HandleClosingAsync()
    {
        var saveState = true;
        for (var indexConnection = _viewModel.Connections.Count - 1; indexConnection >= 0; indexConnection--)
        {
            var connection = _viewModel.Connections[indexConnection];
            _viewModel.SelectedTabConnectionIndex = indexConnection;
            await Task.Delay(100);
            var isTabClosed = await _viewModel.CloseTabConnectionCommand.Execute(connection).ToTask();
            if (isTabClosed) continue;
            saveState = false;
        }

        if (saveState)
        {
            _windowStateService.Save(this);
            this.Closing -= OnClosing;
            this.Close();
        }
    }
}
