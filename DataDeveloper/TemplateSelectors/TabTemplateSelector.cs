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
        var serviceProvider = (App.Current as App).ServiceProvider;
        _viewResolver = serviceProvider.GetService<IViewResolverService>();
    }

    private Dictionary<Guid, Control> _controls = new();
    
    public Control? Build(object? param)
    {
        if (param is not BaseTabContent tab)
            return null;

        if (_controls.ContainsKey(tab.Id)) return _controls[tab.Id];
        
        var view = _viewResolver.ResolveByModel(tab);
        
        if (view == null) 
            view = new TextBlock { Text = "Undefined type", VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };

        _controls.Add(tab.Id, view);

        return _controls[tab.Id];
    }

    public void RemoveControl(BaseTabContent tab)
    {
        _controls.Remove(tab.Id);
    }

    public bool Match(object? data)
    {
        return data is BaseTabContent;
    }
}