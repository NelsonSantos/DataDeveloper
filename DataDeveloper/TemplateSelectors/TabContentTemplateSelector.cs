using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using DataDeveloper.Enums;
using DataDeveloper.Models;
using DataDeveloper.Views;

namespace DataDeveloper.TemplateSelectors;

public class TabContentTemplateSelector : IDataTemplate
{
    private Dictionary<Guid, Control> controls = new Dictionary<Guid, Control>();
    
    public Control? Build(object? param)
    {
        if (param is not TabResult tab)
            return null;

        if (!controls.ContainsKey(tab.Id))
        {
            var control = default(Control);
            switch (tab.Type)
            {
                case TabResultType.Message:
                    control = new MessageView();
                    break;
                case TabResultType.DataGrid:
                    control = new ResultView();
                    break;
                default:
                    control = new TextBox { Text = "Undefined type", VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
                    break;
            }
            controls.Add(tab.Id, control);
        }
        
        return controls[tab.Id];
    }

    public void RemoveControl(TabResult tab)
    {
        controls.Remove(tab.Id);
    }

    public bool Match(object? data)
    {
        return data is TabResult;
    }
}