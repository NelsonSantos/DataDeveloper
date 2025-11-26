using Microsoft.Extensions.DependencyInjection;
using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using DataDeveloper.Interfaces;

namespace DataDeveloper.Services;

public class ViewResolverService : IViewResolverService
{
    private readonly IServiceCollection _services;
    private readonly Dictionary<Type, Type> _map = new();
    private IServiceProvider _provider;
    public ViewResolverService(IServiceCollection services)
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
        _services.AddTransient(viewType);
    }

    public Control ResolveByModel(object viewModel)
    {
        var vmType = viewModel.GetType();
        if (_map.TryGetValue(vmType, out var viewType))
        {
            var view = (Control)_provider.GetService(viewType);
            view.DataContext = viewModel;
            return view;
        }

        return null; //throw new InvalidOperationException($"There are no registered view for type {vmType.FullName}");
    }

    public Control ResolveByType<TType>()
    {
        return ResolveByType(typeof(TType));
    }

    public Control ResolveByType(Type viewType)
    {
        foreach (var pair in _map)
        {
            if (pair.Value != viewType) continue;
            var model = _provider.GetService(pair.Key);
            return ResolveByModel(model);
        }

        return null;
    }
}
