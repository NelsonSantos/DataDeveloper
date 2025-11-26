using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using DataDeveloper.Enums;
using DataDeveloper.Models;
using DataDeveloper.Views;

namespace DataDeveloper.TemplateSelectors;

public class TabResultTemplateSelector : IDataTemplate
{
    private Dictionary<Guid, Control> _controls = new();
    
    public Control? Build(object? param)
    {
        if (param is not BaseTabContent tab)
            return null;

        if (!_controls.ContainsKey(tab.Id))
        {
            var control = default(Control);
            switch (tab.Type)
            {
                case TabType.Message:
                    control = new MessageView();
                    break;
                case TabType.DataGrid:
                    control = new ResultView();
                    break;
                default:
                    control = new TextBox { Text = "Undefined type", VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
                    break;
            }
            _controls.Add(tab.Id, control);
        }
        
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