using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using DataDeveloper.Interfaces;
using DataDeveloper.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DataDeveloper.Services;

public class SchemaCompareDialogService : ISchemaCompareDialogService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ViewLocatorService _viewLocator;

    public SchemaCompareDialogService(IServiceProvider serviceProvider, ViewLocatorService viewLocator)
    {
        _serviceProvider = serviceProvider;
        _viewLocator = viewLocator;
    }

    public async Task ShowDialogAsync(Window parentWindow)
    {
        var model = _serviceProvider.GetRequiredService<SchemaCompareViewModel>();

        var dialog = _viewLocator.Build(model) as Window
                     ?? throw new InvalidOperationException("Schema compare window could not be resolved.");

        await dialog.ShowDialog(parentWindow);
    }
}
