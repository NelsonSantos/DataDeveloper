using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AvaloniaEdit.Highlighting;
using DataDeveloper.Interfaces;
using DataDeveloper.Services;
using DataDeveloper.ViewModels;
using DataDeveloper.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DataDeveloper;

public partial class App : Application
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {    
        SyntaxLoaderService.RegisterSqlHighlighting();
        
        var services = new ServiceCollection();
        var viewResolver = new ViewResolverService(services);

        services.AddSingleton<ViewResolverService>(provider => viewResolver);

        var viewLocator = new ViewLocatorService(viewResolver);
        
        services.AddSingleton<ViewLocatorService>(provider => viewLocator);

        this.RegisterServices(services);
        this.RegisterViewViewModel(viewResolver);
        
        ServiceProvider = services.BuildServiceProvider();
        
        viewResolver.SetServiceProvider(ServiceProvider);
        
        this.DataTemplates.Add(new ViewLocatorService(viewResolver));
        
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow(ServiceProvider)
            {
                DataContext = new MainWindowViewModel(ServiceProvider),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<AppDataFileService>();
        services.AddTransient<IConnectionDialogService, ConnectionDialogService>();
        services.AddSingleton<IWindowStateService, WindowStateService>();
    }

    private void RegisterViewViewModel(IViewResolverService resolver)
    {
        resolver.Register<ConnectionDetailsViewModel, ConnectionDetails>();
        resolver.Register<EditorDocumentViewModel, SqlEditorView>();
        resolver.Register<ResultViewModel, ResultView>();
        resolver.Register<MessageViewModel, MessageView>();
        resolver.Register<ConnectionSelectorViewModel, ConnectionSelectorDialog>();
    }
}