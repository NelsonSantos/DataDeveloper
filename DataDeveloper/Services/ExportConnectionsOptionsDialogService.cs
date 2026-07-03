using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using DataDeveloper.Interfaces;
using DataDeveloper.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DataDeveloper.Services;

public class ExportConnectionsOptionsDialogService : IExportConnectionsOptionsDialogService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ViewLocatorService _viewLocator;

    public ExportConnectionsOptionsDialogService(IServiceProvider serviceProvider, ViewLocatorService viewLocator)
    {
        _serviceProvider = serviceProvider;
        _viewLocator = viewLocator;
    }

    public async Task<ExportConnectionsOptionsResult?> ShowDialogAsync(Window parentWindow)
    {
        var model = _serviceProvider.GetRequiredService<ExportConnectionsOptionsViewModel>();
        var dialog = _viewLocator.Build(model) as Window
                     ?? throw new InvalidOperationException("Export options dialog view could not be resolved.");

        var confirmed = await dialog.ShowDialog<bool?>(parentWindow);
        return confirmed == true ? new ExportConnectionsOptionsResult(model.IncludePasswords) : null;
    }
}
