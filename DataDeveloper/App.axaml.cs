using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AvaloniaEdit.Highlighting;
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
        SyntaxLoader.RegisterSqlHighlighting();
        
        var services = new ServiceCollection();
        var viewResolver = new ViewResolver(services);

        this.RegisterServices(services);
        this.RegisterViewViewModel(viewResolver);
        
        ServiceProvider = services.BuildServiceProvider();
        
        viewResolver.SetServiceProvider(ServiceProvider);
        
        this.DataTemplates.Add(new ViewLocator(viewResolver));
        
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void RegisterServices(IServiceCollection services)
    {
    }

    private void RegisterViewViewModel(IViewResolver resolver)
    {
        resolver.Register<ConnectionDetailsViewModel, ConnectionDetails>();
        resolver.Register<EditorDocumentViewModel, SqlEditorView>();
        resolver.Register<ResultViewModel, ResultView>();
        resolver.Register<MessageViewModel, MessageView>();
    }
}