using Microsoft.Extensions.DependencyInjection;

namespace DataDeveloper.Services;

using Avalonia.Controls;
using System;
using System.Collections.Generic;

public class ViewResolver : IViewResolver
{
    private readonly IServiceCollection _services;
    private readonly Dictionary<Type, Type> _map = new();
    private IServiceProvider _provider;
    public ViewResolver(IServiceCollection services)
    {
        _services = services;
    }

    internal void SetServiceProvider(IServiceProvider provider) => _provider = provider;
    
    public void Register<TViewModel, TView>()
        where TView : Control, new()
    {
        var viewModelType = typeof(TViewModel);
        var viewType = typeof(TView);
        _map[viewModelType] = viewType;
        _services.AddTransient(viewModelType);
    }

    public Control Resolve(object viewModel)
    {
        var vmType = viewModel.GetType();
        if (_map.TryGetValue(vmType, out var viewType))
        {
            var view = (Control)Activator.CreateInstance(viewType)!;
            view.DataContext = viewModel;
            return view;
        }

        throw new InvalidOperationException($"There are no registered view for type {vmType.FullName}");
    }
}
