using System;
using Avalonia.Controls;
using DataDeveloper.Interfaces;
using DataDeveloper.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DataDeveloper.Views;

public partial class MainWindow : Window
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IWindowStateService _windowStateService;

    public MainWindow(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        InitializeComponent();
        _windowStateService = _serviceProvider.GetService<IWindowStateService>();
        _windowStateService.Restore(this);
        DataContext = new MainWindowViewModel(_serviceProvider);
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        _windowStateService.Save(this);
        base.OnClosing(e);
    }
}