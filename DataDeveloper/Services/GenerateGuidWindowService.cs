using System;
using Avalonia.Controls;
using DataDeveloper.Interfaces;
using DataDeveloper.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DataDeveloper.Services;

public class GenerateGuidWindowService : IGenerateGuidWindowService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ViewLocatorService _viewLocator;

    public GenerateGuidWindowService(IServiceProvider serviceProvider, ViewLocatorService viewLocator)
    {
        _serviceProvider = serviceProvider;
        _viewLocator = viewLocator;
    }

    public void Show(Window parentWindow)
    {
        var model = _serviceProvider.GetRequiredService<GenerateGuidViewModel>();
        var window = _viewLocator.Build(model) as Window
                     ?? throw new InvalidOperationException("Generate GUID window could not be resolved.");

        window.Show(parentWindow);
    }
}
