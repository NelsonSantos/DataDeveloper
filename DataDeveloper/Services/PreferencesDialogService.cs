using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using DataDeveloper.Interfaces;
using DataDeveloper.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DataDeveloper.Services;

public class PreferencesDialogService : IPreferencesDialogService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ViewLocatorService _viewLocator;

    public PreferencesDialogService(IServiceProvider serviceProvider, ViewLocatorService viewLocator)
    {
        _serviceProvider = serviceProvider;
        _viewLocator = viewLocator;
    }

    public async Task ShowAsync(Window? parentWindow)
    {
        var model = _serviceProvider.GetRequiredService<PreferencesViewModel>();
        var dialog = _viewLocator.Build(model) as Window
                     ?? throw new InvalidOperationException("Preferences dialog view could not be resolved.");

        if (parentWindow is not null)
            await dialog.ShowDialog<bool?>(parentWindow);
        else
            dialog.Show();
    }
}
