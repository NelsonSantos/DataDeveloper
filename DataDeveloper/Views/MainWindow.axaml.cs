using System;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using DataDeveloper.Interfaces;
using DataDeveloper.Services;
using DataDeveloper.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DataDeveloper.Views;

public partial class MainWindow : Window, IMainWindow
{
    private readonly IServiceScopeFactory _scopeFactory;
    private IServiceScope? _currentScope;
    private MainWindowViewModel _viewModel;
    private readonly IWindowStateService _windowStateService;
    private Guid Id { get; } = Guid.NewGuid();
    public MainWindow(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        _currentScope = _scopeFactory.CreateScope();
        
        InitializeComponent();
        _windowStateService = _currentScope.ServiceProvider.GetService<IWindowStateService>();
        _windowStateService?.Restore(this);
        _viewModel = _currentScope.ServiceProvider.GetService<MainWindowViewModel>();
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

    public IDialogService GetDialogService()
    {
        Console.WriteLine($"Current id: {this.Id}");
        return new DialogService(this);
    }
}