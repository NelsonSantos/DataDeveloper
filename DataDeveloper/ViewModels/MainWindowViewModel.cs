using Dock.Model.Core;
using System;
using System.Reactive;
using Avalonia;
using DataDeveloper.Interfaces;
using DataDeveloper.Services;
using DataDeveloper.Views;
using Dock.Model.Controls;
using ReactiveUI;
using Microsoft.Extensions.DependencyInjection;
    
namespace DataDeveloper.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConnectionDialogService _connectionDialogService;
    private IRootDock? _layout;
    private readonly IFactory? factory; 
    public IRootDock? Layout
    {
        get => _layout;
        set => this.RaiseAndSetIfChanged(ref _layout, value);
    }
    
    public MainWindowViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _connectionDialogService = _serviceProvider.GetService<IConnectionDialogService>();
        
        factory = new DockFactoryService();
        Layout = factory.CreateLayout();
        if (Layout is { } root)
        {
            factory.InitLayout(Layout);
        }

        this.NewWindowCommand = ReactiveCommand.Create(() =>
        {
            var newWindow = new MainWindow(_serviceProvider);
            newWindow.Show();
        });
        this.NewConnection = ReactiveCommand.CreateFromTask<StyledElement>(async (control) =>
        {
            var window = ServicesExtensionMethods.GetParentWindow(control);
            var teste = await _connectionDialogService.ShowDialogAsync(window);
            var a = 10;
        });
    }
    
    public ReactiveCommand<Unit, Unit> NewWindowCommand { get; }
    public ReactiveCommand<StyledElement, Unit> NewConnection { get; }

}