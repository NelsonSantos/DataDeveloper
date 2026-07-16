using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
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
    private static readonly TimeSpan SplashMinimumDuration = TimeSpan.FromSeconds(2);
    public IServiceProvider ServiceProvider { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {    
        SyntaxLoaderService.RegisterSqlHighlighting();
        SyntaxLoaderService.RegisterJsonHighlighting();
        SyntaxLoaderService.RegisterXmlHighlighting();
        
        var services = new ServiceCollection();
        var viewResolver = new ViewResolverService(services);

        services.AddSingleton<IViewResolverService, ViewResolverService>(provider => viewResolver);

        var viewLocator = new ViewLocatorService(viewResolver);
        
        services.AddSingleton<ViewLocatorService>(provider => viewLocator);
        services.AddTransient<IMainWindow, MainWindow>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IReleaseUpdateService, ReleaseUpdateService>();
        services.AddTransient<IStructuredTextCellDialogService, StructuredTextCellDialogService>();

        this.RegisterServices(services);
        this.RegisterViewViewModel(viewResolver);
        
        ServiceProvider = services.BuildServiceProvider();
        
        DatabaseExtensionsMethods.SetServiceProvider(ServiceProvider);
        
        viewResolver.SetServiceProvider(ServiceProvider);
        
        this.DataTemplates.Add(new ViewLocatorService(viewResolver));
        
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _ = ShowMainWindowAsync(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void RegisterServices(IServiceCollection services)
    {
        services.AddTransient<MainWindowViewModel>();
        services.AddSingleton<IEventAggregatorService, EventAggregatorService>();
        services.AddSingleton<AppDataFileService>();
        services.AddSingleton<ISecretStore, PlatformSecretStore>();
        services.AddSingleton<IConnectionSettingsRepository, SqliteConnectionSettingsRepository>();
        services.AddSingleton<IConnectionGroupRepository, SqliteConnectionGroupRepository>();
        services.AddTransient<IConnectionDialogService, ConnectionDialogService>();
        services.AddTransient<IConnectionGroupDialogService, ConnectionGroupDialogService>();
        services.AddTransient<IConnectionExportService, ConnectionExportService>();
        services.AddTransient<IConnectionImportService, ConnectionImportService>();
        services.AddTransient<IExportConnectionsOptionsDialogService, ExportConnectionsOptionsDialogService>();
        services.AddTransient<IGenerateGuidWindowService, GenerateGuidWindowService>();
        services.AddTransient<GenerateGuidViewModel>();
        services.AddSingleton<IWindowStateService, WindowStateService>();
        services.AddSingleton<ISessionTabStore, SessionTabStore>();
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
        resolver.Register<ManageConnectionGroupsViewModel, ManageConnectionGroupsDialog>();
        resolver.Register<ExportConnectionsOptionsViewModel, ExportConnectionsOptionsDialog>();
        resolver.Register<GenerateGuidViewModel, GenerateGuidWindow>();
    }

    private async void OnAboutMenuClick(object? sender, EventArgs e)
    {
        var version = ApplicationVersionHelper.GetCurrentVersion(typeof(App).Assembly);

        var dialogService = ServiceProvider.GetRequiredService<IDialogService>();
        var releaseUpdateService = ServiceProvider.GetRequiredService<IReleaseUpdateService>();
        await dialogService.ShowAboutAsync(version, () => releaseUpdateService.CheckForUpdatesAsync());
    }

    private async void OnMainWindowOpened(object? sender, EventArgs e)
    {
        if (sender is Window window)
            window.Opened -= OnMainWindowOpened;

        var releaseUpdateService = ServiceProvider.GetRequiredService<IReleaseUpdateService>();
        await releaseUpdateService.NotifyIfUpdateAvailableAsync();
    }

    private void OnQuitMenuClick(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }

    private async Task ShowMainWindowAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var version = ApplicationVersionHelper.GetCurrentVersion(typeof(App).Assembly);
        var mainWindow = (MainWindow)ServiceProvider.GetRequiredService<IMainWindow>();

        var splashWindow = new SplashWindow(version);
        PositionSplashWindow(splashWindow, mainWindow);
        splashWindow.Show();

        await Task.Delay(SplashMinimumDuration);

        mainWindow.Opened += OnMainWindowOpened;
        desktop.MainWindow = mainWindow;
        mainWindow.Show();
        splashWindow.Close();
    }

    private static void PositionSplashWindow(Window splashWindow, Window mainWindow)
    {
        var splashX = mainWindow.Position.X + (int)Math.Round((mainWindow.Width - splashWindow.Width) / 2d);
        var splashY = mainWindow.Position.Y + (int)Math.Round((mainWindow.Height - splashWindow.Height) / 2d);
        splashWindow.Position = new PixelPoint(splashX, splashY);
    }
}
