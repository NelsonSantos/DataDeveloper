using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using DataDeveloper.Core;
using DataDeveloper.Enums;
using DataDeveloper.Interfaces;
using DataDeveloper.Models;
using DataDeveloper.ViewModels;
using DataDeveloper.Views;
using Microsoft.Extensions.DependencyInjection;
using TabConnectionViewModel = DataDeveloper.ViewModels.TabConnectionViewModel;

namespace DataDeveloper.TemplateSelectors;

public class TabTemplateSelector : IDataTemplate
{
    private readonly IViewResolverService _viewResolver;

    public TabTemplateSelector()
    {
        var app = App.Current as App ?? throw new InvalidOperationException("App is not initialized.");
        _viewResolver = app.ServiceProvider.GetRequiredService<IViewResolverService>();
    }

    private Dictionary<Guid, Control> _controls = new();
    
    public Control? Build(object? param)
    {
        if (param is not BaseTabContent tab)
            return null;

        if (!_controls.TryGetValue(tab.Id, out var view))
        {
            view = _viewResolver.ResolveByModel(tab);
            _controls.Add(tab.Id, view);
        }

        // Presenters may rebuild while the view is still attached (tab moved to another window, dock document
        // floated): hand out a fresh host each time instead of the view itself.
        return CachedViewHost.Present(view);
    }

    /// <summary>The view already built for <paramref name="tab"/>, if any.</summary>
    public Control? GetCachedControl(BaseTabContent tab) => _controls.GetValueOrDefault(tab.Id);

    public void RemoveControl(BaseTabContent tab)
    {
        _controls.Remove(tab.Id);
    }

    public bool Match(object? data)
    {
        return data is BaseTabContent;
    }
}
