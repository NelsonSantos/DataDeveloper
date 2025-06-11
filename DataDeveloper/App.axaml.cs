using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DataDeveloper.Core;
using DataDeveloper.Data;
using DataDeveloper.Data.Services;
using DataDeveloper.Interfaces;
using DataDeveloper.Services;
using DataDeveloper.ViewModels;
using DataDeveloper.Views;
using Microsoft.Extensions.DependencyInjection;
using TabConnectionViewModel = DataDeveloper.ViewModels.TabConnectionViewModel;

namespace DataDeveloper;

public partial class App : Application
{
    public IServiceProvider ServiceProvider { get; private set; } = null!;
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {    
        SyntaxLoaderService.RegisterSqlHighlighting();
        
        var services = new ServiceCollection();
        var viewResolver = new ViewResolverService(services);

        services.AddSingleton<IViewResolverService, ViewResolverService>(provider => viewResolver);

        var viewLocator = new ViewLocatorService(viewResolver);
        
        services.AddSingleton<ViewLocatorService>(provider => viewLocator);

        this.RegisterServices(services);
        this.RegisterViewViewModel(viewResolver);
        
        ServiceProvider = services.BuildServiceProvider();
        DatabaseExtensionsMethods.SetServiceProvider(ServiceProvider);
        
        viewResolver.SetServiceProvider(ServiceProvider);
        
        this.DataTemplates.Add(new ViewLocatorService(viewResolver));
        
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow(ServiceProvider);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<IEventAggregatorService, EventAggregatorService>();
        services.AddSingleton<AppDataFileService>();
        services.AddTransient<IConnectionDialogService, ConnectionDialogService>();
        services.AddSingleton<IWindowStateService, WindowStateService>();
        services.AddSingleton<DatabaseProviderFactoryService>();
        services.AddTransient<StatementSplitter>();
    }

    private void RegisterViewViewModel(IViewResolverService resolver)
    {
        resolver.Register<TabConnectionViewModel, TabConnectionView>();
        resolver.Register<TabQueryEditorViewModel, TabQueryEditorView>();
        resolver.Register<TabDataGridViewModel, TabDataGridView>();
        resolver.Register<TabMessageViewModel, TabMessageView>();
        resolver.Register<ConnectionSelectorViewModel, ConnectionSelectorDialog>();
    }
}