using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Interfaces;
using DataDeveloper.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DataDeveloper.Services;

public class ConnectionDialogService : IConnectionDialogService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ViewLocatorService _viewLocator;

    public ConnectionDialogService(IServiceProvider serviceProvider, ViewLocatorService viewLocator)
    {
        _serviceProvider = serviceProvider;
        _viewLocator = viewLocator;
    }

    public async Task<IConnectionSettings?> ShowDialogAsync(Window parentWindow)
    {
        var model = _serviceProvider.GetRequiredService<ConnectionSelectorViewModel>();
        var dialog = _viewLocator.Build(model) as Window
                     ?? throw new InvalidOperationException("Connection dialog view could not be resolved.");

        var result = await dialog.ShowDialog<IConnectionSettings>(parentWindow);

        return result;
    }
}
