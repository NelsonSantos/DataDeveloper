using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using DataDeveloper.Interfaces;
using DataDeveloper.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DataDeveloper.Services;

public class ConnectionGroupDialogService : IConnectionGroupDialogService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ViewLocatorService _viewLocator;

    public ConnectionGroupDialogService(IServiceProvider serviceProvider, ViewLocatorService viewLocator)
    {
        _serviceProvider = serviceProvider;
        _viewLocator = viewLocator;
    }

    public async Task ShowDialogAsync(Window parentWindow)
    {
        var model = _serviceProvider.GetRequiredService<ManageConnectionGroupsViewModel>();
        var dialog = _viewLocator.Build(model) as Window
                     ?? throw new InvalidOperationException("Manage groups dialog view could not be resolved.");

        await dialog.ShowDialog(parentWindow);
    }
}
