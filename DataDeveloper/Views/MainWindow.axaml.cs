using System;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Avalonia.Controls;
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
    }

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
