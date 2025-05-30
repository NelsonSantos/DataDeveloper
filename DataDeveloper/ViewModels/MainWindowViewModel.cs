using Dock.Model.Core;
using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using DataDeveloper.Core;
using DataDeveloper.Interfaces;
using DataDeveloper.Services;
using DataDeveloper.Views;
using Dock.Model.Controls;
using ReactiveUI;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.Fody.Helpers;

namespace DataDeveloper.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConnectionDialogService _connectionDialogService;
    private IRootDock? _layout;
    private readonly IAuxFactory? factory; 

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
            try
            {
                await Task.Delay(100);
                var window = ServicesExtensionMethods.GetParentWindow(control);
                var connectionSettings = await _connectionDialogService.ShowDialogAsync(window);

                if (connectionSettings is not null)
                {
                    await factory.AddNewConnectionCommand.Execute(connectionSettings);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        });
    }
    
    public IRootDock? Layout
    {
        get => _layout;
        set => this.RaiseAndSetIfChanged(ref _layout, value);
    }
    
    public ReactiveCommand<Unit, Unit> NewWindowCommand { get; }
    public ReactiveCommand<StyledElement, Unit> NewConnection { get; }

}