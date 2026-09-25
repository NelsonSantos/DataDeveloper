using System;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
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

        // Tunnel handlers run before the SQL editor, which would treat Ctrl+Tab as indent.
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnPreviewKeyUp, RoutingStrategies.Tunnel);
        Deactivated += (_, _) => _viewModel.Switcher?.Cancel();
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (_viewModel.Switcher is { } switcher)
        {
            HandleSwitcherKey(switcher, e);
            return;
        }

        // macOS consumes Control-Tab before it reaches the app; there the Window menu items open the switcher.
        if (e.Key == Key.Tab && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            _viewModel.ShowSwitcher(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -1 : 1);
            e.Handled = true;
        }
    }

    // While the switcher is open it owns the keyboard: Tab/arrows move, 1-9 pick a connection, Left/Right change
    // column (macOS takes Ctrl+arrows for Spaces, so with Ctrl held use the numbers), Enter/Esc close it.
    private static void HandleSwitcherKey(QuerySwitcherViewModel switcher, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Tab:
                switcher.Move(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -1 : 1);
                break;
            case Key.Down:
                switcher.Move(1);
                break;
            case Key.Up:
                switcher.Move(-1);
                break;
            case Key.Left:
                switcher.ActivateConnectionColumn(true);
                break;
            case Key.Right:
                switcher.ActivateConnectionColumn(false);
                break;
            case >= Key.D1 and <= Key.D9:
                switcher.HighlightConnectionNumber(e.Key - Key.D0);
                break;
            case >= Key.NumPad1 and <= Key.NumPad9:
                switcher.HighlightConnectionNumber(e.Key - Key.NumPad0);
                break;
            case Key.Enter:
                switcher.Commit();
                break;
            case Key.Escape:
                switcher.Cancel();
                break;
            case Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift:
                return;
        }

        e.Handled = true;
    }

    // Releasing Ctrl switches to the highlighted entry (other keys keep it open, e.g. when opened from the menu).
    private void OnPreviewKeyUp(object? sender, KeyEventArgs e)
    {
        if (_viewModel.Switcher is not { } switcher || e.Key is not (Key.LeftCtrl or Key.RightCtrl))
            return;

        switcher.Commit();
        e.Handled = true;
    }

    private void OnShowNextQueryTab(object? sender, EventArgs e) => _viewModel.ShowSwitcher(1);

    private void OnShowPreviousQueryTab(object? sender, EventArgs e) => _viewModel.ShowSwitcher(-1);

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
